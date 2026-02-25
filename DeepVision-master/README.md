# DeepVision — AI Image Cleaner & Text Extractor

A prototype that uses **DeepSeek AI** as the core engine to:

1. **Clean / pre-process** an uploaded image (contrast, brightness, sharpening, denoising via ImageSharp)
2. **Extract text** from the cleaned image using DeepSeek's vision/multimodal API
3. **Display** the original image, the cleaned image, and the extracted text side-by-side in a web UI

---

## Stack

| Layer    | Technology                        |
|----------|-----------------------------------|
| Frontend | Angular 17 (standalone components, signals) |
| Backend  | ASP.NET Core 8 Web API (C#)       |
| AI       | DeepSeek API (chat/vision)        |
| Images   | SixLabors.ImageSharp              |

---

## Project Structure

```
DeepVision/
├── backend/
│   └── DeepVision.Api/
│       ├── Controllers/ImageController.cs   # POST /api/image/process
│       ├── Models/                          # Request/response DTOs
│       ├── Services/
│       │   ├── ImageProcessingService.cs    # ImageSharp cleaning pipeline
│       │   └── DeepSeekService.cs           # DeepSeek API client
│       ├── Program.cs
│       └── appsettings.json                 # ← put your API key here
└── frontend/
    └── deepvision-app/                      # Angular 17 app
        └── src/app/
            ├── components/image-processor/  # Main UI component
            ├── services/image.service.ts
            └── models/image-result.model.ts
```

---

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download)
- [Node.js 18+](https://nodejs.org/) & npm
- [Angular CLI](https://angular.io/cli): `npm install -g @angular/cli`
- A **DeepSeek API key** from [platform.deepseek.com](https://platform.deepseek.com/)

---

## Setup & Run

### 1. Configure the DeepSeek API Key

Edit `backend/DeepVision.Api/appsettings.json`:

```json
"DeepSeek": {
  "ApiKey": "sk-xxxxxxxxxxxxxxxxxxxxxxxx",
  "BaseUrl": "https://api.deepseek.com",
  "Model": "deepseek-chat",
  ...
}
```

> **Note on models**: If your DeepSeek account has access to a vision model
> (e.g. `deepseek-vl2`), set `"Model": "deepseek-vl2"` for best OCR results.
> The default `deepseek-chat` also supports image inputs via its multimodal API.

### 2. Run the Backend

```bash
cd backend/DeepVision.Api
dotnet restore
dotnet run
# API available at: http://localhost:5000
# Swagger UI:       http://localhost:5000/swagger
```

### 3. Run the Frontend

```bash
cd frontend/deepvision-app
npm install
ng serve
# App available at: http://localhost:4200
```

---

## API Reference

### `POST /api/image/process`

Upload an image for cleaning and text extraction.

**Request**: `multipart/form-data`
- `image` (file) — the image to process

**Response**: `application/json`

```json
{
  "success": true,
  "originalImageBase64": "...",
  "processedImageBase64": "...",
  "imageMimeType": "image/png",
  "extractedText": "Hello World\nThis is extracted text...",
  "metadata": {
    "originalWidth": 1920,
    "originalHeight": 1080,
    "processedWidth": 1920,
    "processedHeight": 1080,
    "format": "PNG",
    "fileSizeBytes": 204800,
    "processingSteps": [
      "Loaded image: 1920x1080",
      "Auto-oriented (EXIF correction)",
      "Applied contrast enhancement...",
      "..."
    ],
    "tokensUsed": 312
  }
}
```

### `GET /api/image/health`

Returns `{ "status": "ok" }`.

---

## Image Cleaning Pipeline

The backend applies these steps before sending the image to DeepSeek:

1. **Auto-orient** — corrects EXIF rotation
2. **Resize** — scales down to max 2048px (longest side) to keep API calls efficient
3. **Contrast +15%, Brightness +5%** — improves text visibility
4. **Sharpen** — sharpens edges for cleaner character recognition
5. **Mild denoise** — Gaussian blur (σ=0.4) to reduce noise
6. **Re-sharpen** — recovers crispness after denoising
7. **Encode as PNG** — lossless output for best quality

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `Network error — is the backend running?` | Start the backend with `dotnet run` |
| `DeepSeek API returned 401` | Check your API key in `appsettings.json` |
| `DeepSeek API returned 400` | The model may not support image inputs; try `deepseek-vl2` |
| CORS error in browser | Ensure the backend allows `http://localhost:4200` (already configured) |
| File too large | Max upload is 20 MB |
