export interface DocumentDetails {
  documentType?: string | null;
  invoiceNumber?: string | null;
  invoiceDate?: string | null;
  dueDate?: string | null;
  vendorName?: string | null;
  customerName?: string | null;
  subTotal?: string | null;
  taxAmount?: string | null;
  totalAmount?: string | null;
  currency?: string | null;
  paymentTerms?: string | null;
  additionalFields?: Record<string, string> | null;
}

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
  documentDetails?: DocumentDetails | null;
}

export type ProcessingState = 'idle' | 'uploading' | 'processing' | 'done' | 'error';
