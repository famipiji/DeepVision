export interface ImageMetadata {
  originalWidth: number;
  originalHeight: number;
  processedWidth: number;
  processedHeight: number;
  format: string;
  fileSizeBytes: number;
  processingSteps: string[];
  tokensUsed: number;
  pageCount: number;
  processedPageCount: number;
}

export interface ProcessImageResponse {
  success: boolean;
  errorMessage?: string;
  originalImageBase64?: string;
  processedImageBase64?: string;
  imageMimeType?: string;
  extractedText?: string;
  metadata?: ImageMetadata;
}

export type ProcessingState = 'idle' | 'uploading' | 'processing' | 'done' | 'error';
