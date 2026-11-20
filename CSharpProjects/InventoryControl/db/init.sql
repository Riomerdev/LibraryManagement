CREATE TABLE IF NOT EXISTS products (
 id uuid PRIMARY KEY, sku varchar(40) NOT NULL, name varchar(160) NOT NULL,
 region varchar(24) NOT NULL, on_hand integer NOT NULL DEFAULT 0 CHECK (on_hand >= 0),
 reorder_point integer NOT NULL DEFAULT 0 CHECK (reorder_point >= 0), version integer NOT NULL DEFAULT 1,
 CONSTRAINT uq_products_sku_region UNIQUE (sku, region)
);
CREATE TABLE IF NOT EXISTS stock_movements (
 id uuid PRIMARY KEY, product_id uuid NOT NULL REFERENCES products(id) ON DELETE RESTRICT,
 kind varchar(12) NOT NULL CHECK (kind IN ('RECEIPT', 'ISSUE')),
 quantity_delta integer NOT NULL CHECK (quantity_delta <> 0), reason varchar(240) NOT NULL,
 idempotency_key varchar(100) NOT NULL UNIQUE, occurred_at timestamptz NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_stock_movements_product_time ON stock_movements (product_id, occurred_at DESC);
INSERT INTO products (id, sku, name, region, on_hand, reorder_point, version)
VALUES
 ('8d0fe9e3-f541-4bf2-9d27-38d00e7b12a1', 'BR-1042', 'Sensor de temperatura', 'BR-SC', 28, 18, 1),
 ('0ff4e90a-ea0f-4869-b95a-5e1e03c661e7', 'US-2081', 'Industrial gateway', 'US-EAST', 9, 14, 1),
 ('148d0c54-0b3c-4605-bcc3-57df4d96161a', 'EU-3310', 'Power module', 'EU-WEST', 44, 20, 1)
ON CONFLICT (sku, region) DO NOTHING;
