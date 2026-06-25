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
LogSeqDBExport --db <path/to/db.sqlite> --out <output-directory>
```

Example:

```bash
LogSeqDBExport --db "C:\Users\you\logseq\db.sqlite" --out "C:\Temp\logseq-export"
```

### Useful options

- `--db <sqlite-file>`: Path to the LogSeq SQLite database
- `--out <output-dir>`: Output directory for generated Markdown files
- `--table <table-name>`: Table to read from (default: `kvs`)
- `--query <sql>`: Use a custom SQL query instead of the default `select content from {table}`
- `--includeBlockProps true|false`: Include block properties in output (default: `true`)
- `--resolveAliases true|false`: Resolve LogSeq block/page aliases when rendering links and page titles (default: `true`)
- `--excludeDeleted true|false`: Exclude deleted entities from output (default: `true`)
- `--exportOnlyPageChildren true|false`: Export only direct page children (not transitive descendants) (default: `false`)
- `--usePropertyForFolder <property-key>`: Create output folders based on a mapped property value for each root page
- `--intermediateFile <file>`: Write intermediate JSON to a file (for debug purpose)
- `--dboutputFile <file>`: Write raw database export JSON to a file (for debug purpose)
- `--finalFile <file>`: Write final entity JSON to a file (for debug purpose)
- `-h, --help`: Show help

## Configuration

The export behavior is controlled by `config/config.json`.

This file defines:

- `DefaultImageWidth`: width used for rendered asset links
- `Exclusions`: page titles or IDs to ignore during export
- `Mappings`: advanced mapping rules for any incoming property key
- `DateMapping`: regex-based date conversion rules for page titles (if your LogSeq wasn't configured as expected in your target app). Please note this is limited (since using Regex) to inverting day / month / year and isn't an actual date parser. Might change that if requested.

### `Mappings` support

Each mapping entry is keyed by an incoming LogSeq property name, such as a block or user property key.

Supported fields in a mapping entry:

- `Target`: the output property name written into the page YAML front matter
- `FallbackTarget`: a fallback output key used when a property value does not match any `ValueMappings`
- `Format`: a .NET composite format string used to render mapped values
- `ValueMappings`: map specific source values to alternate output values
- `Scope`: `Block` or `Page` to control whether the mapping applies only to page entities
- `Mode`: `Property`, `Append`, or `Prepend` to control whether the value becomes a YAML property or is added to the page title/content

### Example behavior

- `Property` mode writes a front-matter field.
- `Append` and `Prepend` modes inject formatted values into the page title or block contents.
- `ValueMappings` can normalize tags, statuses, priorities, or custom property values.
- `UnmappedTarget` preserves unmatched values in a secondary field.

Adjust `config/config.json` to match your LogSeq schema and property naming conventions.

### Example `Mappings` entry

```json
"Mappings": {
  "~:block/tags": {
    "Target": "type",
    "UnmappedTarget": "tags",
    "ValueMappings": {
      "Person": "person",
      "Company": "company",
      "Journal": "journal"
    }
  },
  "~:logseq.property/status": {
    "Mode": "prepend",
    "ValueMappings": {
      "Todo": "[ ]",
      "Done": "[X]"
    }
  },
  "~:logseq.property/deadline": {
    "Mode": "append",
    "Format": " 📅 {0:yyyy-MM-dd}"
  }
}
```

This example shows how a source property can be mapped into a front-matter field, normalized with `ValueMappings`, or appended/prepended to the rendered content.

## Output

The tool renders one Markdown file per root page. Each file contains YAML front matter for mapped page properties and a nested list of child blocks.

If `--usePropertyForFolder <property-key>` is provided, exported pages are grouped into subfolders using the mapped property value for each root page.

## Notes

- The project is intended to support LogSeq databases that store content in SQLite.
- The tool is not designed for plain markdown-only LogSeq files.
- `config/config.json` is the main place to customize exports for your own LogSeq setup.

## License

This repository is released under the terms of the `LICENSE` file.
