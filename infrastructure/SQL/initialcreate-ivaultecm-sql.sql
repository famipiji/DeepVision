-- 1. Create the Users table (Matches User_2.cs and AppDbContext_3.cs mapping)
CREATE TABLE IF NOT EXISTS "users" (
    "id" uuid NOT NULL DEFAULT gen_random_uuid(),
    "username" text NOT NULL,
    "email" text,
    "password" text NOT NULL, -- Mapped to PasswordHash property in C#
    "created_at" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "pk_users" PRIMARY KEY ("id"),
    CONSTRAINT "uq_users_username" UNIQUE ("username")
);

-- 2. Create the Record Definitions table (Matches RecordDefinition_2.cs)
CREATE TABLE IF NOT EXISTS "record_definitions" (
    "id" uuid NOT NULL DEFAULT gen_random_uuid(),
    "name" character varying(100) NOT NULL,
    "description" text,
    "field_schema" jsonb NOT NULL DEFAULT '[]',
    "created_at" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "pk_record_definitions" PRIMARY KEY ("id")
);

-- 3. Create the Records table (Matches Record_3.cs)
CREATE TABLE IF NOT EXISTS "records" (
    "id" uuid NOT NULL DEFAULT gen_random_uuid(),
    "record_definition_id" uuid NOT NULL,
    "metadata" jsonb NOT NULL,
    "created_at" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    "updated_at" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "pk_records" PRIMARY KEY ("id"),
    CONSTRAINT "fk_records_definitions" FOREIGN KEY ("record_definition_id") 
        REFERENCES "record_definitions" ("id") ON DELETE CASCADE
);

-- 4. Create the Contents table (Matches Content_3.cs)
CREATE TABLE IF NOT EXISTS "contents" (
    "id" uuid NOT NULL,
    "record_id" uuid NOT NULL,
    "version_number" integer NOT NULL DEFAULT 1,
    "file_id" text NOT NULL,
    "file_path" text NOT NULL,
    "file_name" text NOT NULL,
    "is_latest" boolean NOT NULL DEFAULT TRUE,
    "ocr_status" text NOT NULL DEFAULT 'Pending',
    "raw_ocr_content" text,
    "ocr_coordinates_json" text,
    "ocr_completed_at" timestamp with time zone,
    "created_at" timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT "pk_contents" PRIMARY KEY ("id")
);

-- 5. Internal EF Core tracking
CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "migration_id" character varying(150) NOT NULL,
    "product_version" character varying(32) NOT NULL,
    CONSTRAINT "pk___ef_migrations_history" PRIMARY KEY ("migration_id")
);

-- 6. Insert Default Admin and Record Types
INSERT INTO "users" ("username", "email", "password") 
VALUES ('admin', 'admin@ivault.local', 'Admin@123');

INSERT INTO "record_definitions" ("name", "description", "field_schema")
VALUES 
('Employee', 'Staff metadata profiles', '{"fields": [{"name": "staff_id", "type": "alphanumeric"}, {"name": "department", "type": "dropdown", "options": ["IT", "HR", "Finance"]}]}'),
('Invoice', 'Billing documents', '{"fields": [{"name": "invoice_no", "type": "text"}, {"name": "amount", "type": "numeric"}]}');

-- 7. Mark as Initialized
INSERT INTO "__EFMigrationsHistory" ("migration_id", "product_version")
VALUES ('20260502_ManualRebuild', '8.0.0');