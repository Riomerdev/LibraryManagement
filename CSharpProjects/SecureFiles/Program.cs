using System.Security.Cryptography;
using Portfolio.Storage;

var builder = WebApplication.CreateBuilder(args);
var path = builder.Configuration["DATA_FILE"] ?? Path.Combine(builder.Environment.ContentRootPath, "data", "files.json");
builder.Services.AddSingleton(new JsonStore<FileState>(path));
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseExceptionHandler();
var apiKey = builder.Configuration["SECUREFILES_API_KEY"] ?? Environment.GetEnvironmentVariable("SECUREFILES_API_KEY");
var storageRoot = builder.Configuration["UPLOAD_DIRECTORY"] ?? Path.Combine(app.Environment.ContentRootPath, "uploads");
app.Use(async (context, next) =>
{
    if (context.Request.Path == "/health") { await next(); return; }
    if (string.IsNullOrWhiteSpace(apiKey))
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { error = "Set SECUREFILES_API_KEY before using the API." });
        return;
    }
    var supplied = context.Request.Headers["X-API-Key"].ToString();
    var expectedBytes = System.Text.Encoding.UTF8.GetBytes(apiKey);
    var suppliedBytes = System.Text.Encoding.UTF8.GetBytes(supplied);
    if (expectedBytes.Length != suppliedBytes.Length ||
        !CryptographicOperations.FixedTimeEquals(expectedBytes, suppliedBytes))
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return;
    }
    await next();
});
app.MapGet("/health", () => Results.Ok(new { status = "healthy", service = "secure-files" }));

app.MapPost("/api/files", async (HttpRequest request, JsonStore<FileState> store, CancellationToken ct) =>
{
    if (!request.HasFormContentType) return Results.BadRequest(new { error = "Use multipart/form-data." });
    var form = await request.ReadFormAsync(ct);
    var organizationId = form["organizationId"].ToString().Trim();
    var file = form.Files.GetFile("file");
    if (organizationId.Length is < 2 or > 80 || file is null || file.Length == 0 || file.Length > 10 * 1024 * 1024)
        return Results.BadRequest(new { error = "Organization and a file of 1 byte to 10 MiB are required." });
    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
    var allowed = new Dictionary<string, (string Mime, byte[] Magic)>
    {
        [".pdf"] = ("application/pdf", "%PDF-"u8.ToArray()),
        [".png"] = ("image/png", [137, 80, 78, 71, 13, 10, 26, 10]),
        [".jpg"] = ("image/jpeg", [255, 216, 255]),
        [".jpeg"] = ("image/jpeg", [255, 216, 255])
    };
    if (!allowed.TryGetValue(extension, out var rule) || file.ContentType != rule.Mime)
        return Results.BadRequest(new { error = "Only PDF, PNG and JPEG files with matching MIME type are accepted." });
    Directory.CreateDirectory(storageRoot);
    var id = Guid.NewGuid();
    var objectName = id.ToString("N") + extension;
    var destination = Path.Combine(storageRoot, objectName);
    using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
    var buffer = new byte[81920];
    long length = 0;
    try
    {
        await using var source = file.OpenReadStream();
        await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            buffer.Length, FileOptions.Asynchronous | FileOptions.WriteThrough);
        var first = true;
        while (true)
        {
            var read = await source.ReadAsync(buffer, ct);
            if (read == 0) break;
            length += read;
            if (length > 10 * 1024 * 1024) throw new InvalidDataException("File exceeds the 10 MiB limit.");
            if (first)
            {
                first = false;
                if (read < rule.Magic.Length || !buffer.AsSpan(0, rule.Magic.Length).SequenceEqual(rule.Magic))
                    throw new InvalidDataException("File signature does not match its extension.");
            }
            hash.AppendData(buffer, 0, read);
            await target.WriteAsync(buffer.AsMemory(0, read), ct);
        }
    }
    catch (InvalidDataException exception)
    {
        if (File.Exists(destination)) File.Delete(destination);
        return Results.BadRequest(new { error = exception.Message });
    }
    var record = new StoredFile(id, organizationId, Path.GetFileName(file.FileName), rule.Mime,
        objectName, length, Convert.ToHexStringLower(hash.GetHashAndReset()), DateTimeOffset.UtcNow);
    await store.UpdateAsync(state => { state.Files.Add(record); return true; }, ct);
    return Results.Created($"/api/files/{id}?organizationId={Uri.EscapeDataString(organizationId)}", record);
});

app.MapGet("/api/files", async (string organizationId, JsonStore<FileState> store, CancellationToken ct) =>
    await store.ReadAsync(state => state.Files.Where(file => file.OrganizationId == organizationId)
        .OrderByDescending(file => file.UploadedAt).ToArray(), ct));

app.MapGet("/api/files/{id:guid}", async (Guid id, string organizationId, JsonStore<FileState> store, CancellationToken ct) =>
{
    var file = await store.ReadAsync(state => state.Files.SingleOrDefault(item => item.Id == id && item.OrganizationId == organizationId), ct);
    if (file is null) return Results.NotFound();
    var pathOnDisk = Path.Combine(storageRoot, file.ObjectName);
    if (!System.IO.File.Exists(pathOnDisk)) return Results.NotFound(new { error = "Stored object is missing." });
    return Results.File(pathOnDisk, file.ContentType, file.OriginalName);
});
app.Run();

public sealed class FileState { public List<StoredFile> Files { get; set; } = []; }
public sealed record StoredFile(Guid Id, string OrganizationId, string OriginalName, string ContentType,
    string ObjectName, long SizeBytes, string Sha256, DateTimeOffset UploadedAt);