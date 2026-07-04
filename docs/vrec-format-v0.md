# VREC Format v0

`.vrec` is a zip package for one recorded machine-vision inspection run.

The v0 goal is boring and inspectable: JSON metadata plus raw image payload files. No database, no custom binary container, and no required server.

## Package Layout

```text
shot.vrec
  manifest.json
  images/
    01_raw.rbuf.json
    01_raw.raw
    02_binary.rbuf.json
    02_binary.raw
  overlays.json
  params.json
  measures.json
  events.json
  exceptions.json
```

Only `manifest.json` is required. Other files are optional and should be omitted when empty.

## manifest.json

```json
{
  "schema": "vrec",
  "version": 0,
  "shotId": "20260704-154841-Cam1-000001",
  "camera": "Cam1",
  "triggerId": "000001",
  "recipe": "Main",
  "startedUtc": "2026-07-04T06:48:41.0000000Z",
  "durationMs": 12.4,
  "result": "NG",
  "software": {
    "name": "CustomerVisionApp",
    "version": "1.2.3",
    "machine": "LINE1-PC"
  },
  "stages": [
    {
      "id": "01_raw",
      "name": "Raw",
      "kind": "image",
      "image": "images/01_raw.rbuf.json",
      "timestampMs": 0.0
    },
    {
      "id": "02_binary",
      "name": "Binary",
      "kind": "image",
      "image": "images/02_binary.rbuf.json",
      "timestampMs": 4.1
    }
  ]
}
```

## Image Files

Images reuse the current `.rbuf.json` model:

```json
{
  "rawFile": "01_raw.raw",
  "width": 2448,
  "height": 2048,
  "stride": 2448,
  "pixelFormat": "Mono8",
  "validBits": 8,
  "byteOrder": "LittleEndian"
}
```

Paths inside image descriptors are relative to the descriptor file.

## params.json

```json
[
  {
    "name": "ExposureUs",
    "value": 8000,
    "type": "double",
    "stageId": "01_raw"
  },
  {
    "name": "Threshold",
    "value": 120,
    "type": "int",
    "stageId": "02_binary"
  }
]
```

## overlays.json

```json
[
  {
    "stageId": "01_raw",
    "name": "SearchROI",
    "kind": "rectangle",
    "color": "#00D7FF",
    "points": [
      { "x": 100.0, "y": 200.0 },
      { "x": 600.0, "y": 200.0 },
      { "x": 600.0, "y": 500.0 },
      { "x": 100.0, "y": 500.0 }
    ]
  }
]
```

Supported v0 overlay kinds:

- `rectangle`
- `polygon`
- `polyline`
- `point`
- `circle`
- `text`

## measures.json

```json
[
  {
    "name": "Width",
    "value": 12.345,
    "unit": "mm",
    "lower": 12.0,
    "upper": 13.0,
    "result": "OK",
    "stageId": "03_measure"
  }
]
```

## events.json

```json
[
  { "name": "PLC Trigger", "timestampMs": 0.0 },
  { "name": "Exposure Start", "timestampMs": 0.7 },
  { "name": "Grab Complete", "timestampMs": 3.2 },
  { "name": "Processing End", "timestampMs": 12.4 }
]
```

## exceptions.json

```json
[
  {
    "stageId": "03_measure",
    "type": "System.InvalidOperationException",
    "message": "No edge found.",
    "stackTrace": "..."
  }
]
```

## Compatibility Rules

- Readers must ignore unknown JSON properties.
- Readers must reject unknown required files only when referenced from `manifest.json`.
- Writers should keep stage IDs stable and sortable.
- Raw payloads should stay uncompressed for very large images unless compression is explicitly requested.
- JSON should be UTF-8.

## v0 Implementation Order

1. Create `VisionRecorder.Sdk` with manifest writing only.
2. Add `AddImage` overloads that write `.rbuf.json` plus `.raw`.
3. Add params, ROIs, measures, events, and exceptions.
4. Add `VisionReplayViewer` open support for one `.vrec`.
5. Add side-by-side comparison for two `.vrec` files.
