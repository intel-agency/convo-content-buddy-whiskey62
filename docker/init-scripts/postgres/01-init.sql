-- ConvoContentBuddy PostgreSQL Initialization Script
-- Creates necessary extensions and schema

-- Enable pgvector extension for vector similarity search
CREATE EXTENSION IF NOT EXISTS vector;

-- Enable UUID extension
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Create schema for application data
CREATE SCHEMA IF NOT EXISTS app;

-- Grant permissions
GRANT ALL PRIVILEGES ON SCHEMA app TO postgres;
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA app TO postgres;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA app TO postgres;

-- Log initialization
DO $$
BEGIN
    RAISE NOTICE 'ConvoContentBuddy database initialized successfully';
END $$;
