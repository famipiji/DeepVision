# DeepVision — AI Image Cleaner & Text Extractor

A prototype that uses **Groq AI** (Llama 3.3 70B) as the core engine to:

1. **Clean / pre-process** an uploaded image (contrast, brightness, sharpening, denoising via ImageSharp)
2. **Extract text** from the cleaned image using Tesseract OCR
3. **Analyse** the extracted text with Groq to identify structured document fields
4. **Display** the original image, the cleaned image, extracted text, and key details side-by-side in a web UI

---

## Screenshots

![Upload screen](docs/screenshots/upload.png)

![Extraction result](docs/screenshots/extraction.png)

![Key details panel](docs/screenshots/key-details.png)

---

## Stack

| Layer    | Technology                                   |
|----------|----------------------------------------------|
| Frontend | Angular 17 (standalone components, signals)  |
| Backend  | ASP.NET Core 8 Web API (C#)                  |
| AI       | Groq API — Llama 3.3 70B Versatile           |
| OCR      | Tesseract 5 (local, offline)                 |
| Images   | SixLabors.ImageSharp                         |

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
│       │   └── DeepSeekService.cs           # Groq API client
│       ├── Program.cs
│       ├── appsettings.json                 # Config (no secrets)
│       └── appsettings.Development.json     # ← put your API key here (gitignored)
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
- A **Groq API key** from [console.groq.com](https://console.groq.com/)

---

## Setup & Run

### 1. Configure the Groq API Key

Create `backend/DeepVision.Api/appsettings.Development.json` (this file is gitignored):

```json
{
  "Groq": {
    "ApiKey": "gsk_your_key_here"
  }
}
```

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

Upload an image or PDF (up to 5 pages) for cleaning and text extraction.

**Request**: `multipart/form-data`
- `image` (file) — JPEG, PNG, WebP, BMP, TIFF, or PDF · max 20 MB

**Response**: `application/json`

```json
{
  "success": true,
  "originalImageBase64": "...",
  "processedImageBase64": "...",
  "imageMimeType": "image/png",
  "extractedText": "GEMILITE SDN. BERHAD INVOICE ...",
  "documentDetails": {
    "documentType": "Invoice",
    "invoiceNumber": "308271",
    "invoiceDate": "05 Jun 13",
    "vendorName": "GEMILITE SDN. BERHAD",
    "customerName": "Energy Formula Sdn Bhd",
    "totalAmount": "1296.90",
    "currency": "MYR",
    "paymentTerms": "120 DAYS"
  },
  "metadata": {
    "originalWidth": 1920,
    "originalHeight": 1080,
    "tokensUsed": 312,
    "pageCount": 1,
    "processedPageCount": 1
  }
}
```

### `GET /api/image/health`

Returns `{ "status": "ok" }`.

---

## Image Cleaning Pipeline

The backend applies these steps before OCR:

1. **Auto-orient** — corrects EXIF rotation
2. **Resize** — scales down to max 2048px (longest side)
3. **Contrast +15%, Brightness +5%** — improves text visibility
4. **Sharpen** — sharpens edges for cleaner character recognition
5. **Mild denoise** — Gaussian blur (σ=0.4) to reduce noise
6. **Re-sharpen** — recovers crispness after denoising
7. **Encode as PNG** — lossless output for best OCR quality

---

## Troubleshooting

| Problem | Solution |
|---------|----------|
| `Network error — is the backend running?` | Start the backend with `dotnet run` |
| `Groq API returned 401` | Check your API key in `appsettings.Development.json` |
| CORS error in browser | Ensure the backend allows `http://localhost:4200` (already configured) |
| File too large | Max upload is 20 MB |
| Key details not showing | Check backend logs; Groq API may have returned an error |
