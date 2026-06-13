# LogSeqDBExport

A small .NET tool for exporting content from a LogSeq SQLite database into Markdown files with YAML front matter.

This repository is designed for the LogSeq database format, not the markdown-based LogSeq workspace. It reads the database, parses blocks and page metadata, resolves references and tags, and renders each root page as a Markdown document.

## What it does

- Reads a LogSeq SQLite database file (`.db`)
- Extracts raw JSON content from the configured table
- Parses LogSeq entities and resolves parent-child relationships
- Transforms properties, tags and references according to `config/config.json`
- Writes Markdown files to an output directory
- Optionally emits raw DB export, intermediate JSON, and final JSON files for debugging

## Requirements

- .NET 10 SDK or later
- A LogSeq SQLite database file (not the markdown-only LogSeq version)

## Usage

From the repository root:

```bash
dotnet run -- --db <path/to/logseq.db> --out <output-directory>
```

Example:

```bash
dotnet run -- --db "C:\Users\you\logseq\logseq.db" --out "C:\Temp\logseq-export"
```

### Useful options

- `--db <sqlite-file>`: Path to the LogSeq SQLite database
- `--out <output-dir>`: Output directory for generated Markdown files
- `--table <table-name>`: Table to read from (default: `kvs`)
- `--query <sql>`: Use a custom SQL query instead of the default `select content from {table}`
- `--includeBlockProps true|false`: Include block properties in output (default: `true`)
- `--ignoreBuiltIn true|false`: Ignore built-in properties (default: `false`)
- `--exportOnlyPageChildren true|false`: Export only immediate page children, not nested descendants (default: `false`)
- `--useTypeForFolder true|false`: Group exported pages into folders by `type` property (default: `false`)
- `--intermediateFile <file>`: Write intermediate JSON to a file
- `--dboutputFile <file>`: Write raw database export JSON to a file
- `--finalFile <file>`: Write final entity JSON to a file
- `-h, --help`: Show help

## Configuration

The export behavior is controlled by `config/config.json`.

This file defines:

- `Exclusions`: page titles or IDs to ignore
- `PropertyMappings`: map LogSeq property keys to output field names
- `PageOnlyPropertyMappings`: properties rendered only for page-level entities
- `ResolvableProps`: properties whose values should be resolved as references
- `TagsToPropertyMappings`: map tag names to additional output properties
- `DateMapping`: regex-based date conversion rules

Adjust `config/config.json` to match your LogSeq schema and property naming conventions.

## Output

The tool renders one Markdown file per root page. Each file contains YAML front matter for mapped page properties and a nested list of child blocks.

If `--useTypeForFolder true` is enabled, exported pages are grouped into subfolders by `type`.

## Notes

- The project is intended to support LogSeq databases that store content in SQLite.
- The tool is not designed for plain markdown-only LogSeq exports.
- `config/config.json` is the main place to customize exports for your own LogSeq setup.

## License

This repository is released under the terms of the `LICENSE` file.
