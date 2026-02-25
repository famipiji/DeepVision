import {
  Component,
  ElementRef,
  ViewChild,
  signal,
  computed
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { ImageService } from '../../services/image.service';
import {
  ProcessImageResponse,
  ProcessingState
} from '../../models/image-result.model';

@Component({
  selector: 'app-image-processor',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './image-processor.component.html',
  styleUrls: ['./image-processor.component.scss']
})
export class ImageProcessorComponent {
  @ViewChild('fileInput') fileInput!: ElementRef<HTMLInputElement>;
  @ViewChild('dropZone') dropZone!: ElementRef<HTMLDivElement>;

  // ── State ────────────────────────────────────────────────────────────────
  state = signal<ProcessingState>('idle');
  result = signal<ProcessImageResponse | null>(null);
  previewSrc = signal<string | null>(null);
  selectedFile = signal<File | null>(null);
  isPdf = signal(false);
  isDragging = signal(false);
  errorMessage = signal<string | null>(null);
  activeTab = signal<'original' | 'processed'>('processed');
  copyLabel = signal('Copy');

  // ── Computed ─────────────────────────────────────────────────────────────
  isIdle = computed(() => this.state() === 'idle');
  isLoading = computed(() => this.state() === 'uploading' || this.state() === 'processing');
  isDone = computed(() => this.state() === 'done');
  isError = computed(() => this.state() === 'error');

  statusLabel = computed(() => {
    const kind = this.isPdf() ? 'PDF' : 'image';
    switch (this.state()) {
      case 'uploading':   return `Uploading ${kind}…`;
      case 'processing':  return 'DeepSeek is extracting text…';
      case 'done':        return 'Done';
      case 'error':       return 'Error';
      default:            return '';
    }
  });

  pageLabel = computed(() => {
    const m = this.result()?.metadata;
    if (!m || m.pageCount <= 1) return null;
    return `${m.processedPageCount} of ${m.pageCount} page(s) processed`;
  });

  displayedImageSrc = computed(() => {
    const r = this.result();
    if (!r) return null;
    const base64 = this.activeTab() === 'processed'
      ? r.processedImageBase64
      : r.originalImageBase64;
    return base64 ? this.imageService.buildImageSrc(base64, r.imageMimeType ?? 'image/png') : null;
  });

  fileSizeLabel = computed(() => {
    const f = this.selectedFile();
    return f ? this.imageService.formatFileSize(f.size) : '';
  });

  constructor(private imageService: ImageService) {}

  // ── File selection ────────────────────────────────────────────────────────
  openFilePicker(): void {
    this.fileInput.nativeElement.click();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.loadFile(file);
    // Reset so the same file can be re-selected
    input.value = '';
  }

  // ── Drag & drop ───────────────────────────────────────────────────────────
  onDragOver(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    this.isDragging.set(false);
    const file = event.dataTransfer?.files?.[0];
    if (file) this.loadFile(file);
  }

  // ── Core flow ─────────────────────────────────────────────────────────────
  private loadFile(file: File): void {
    const pdf = file.type === 'application/pdf';

    if (!file.type.startsWith('image/') && !pdf) {
      this.errorMessage.set('Please select an image (JPEG, PNG, WebP…) or a PDF file.');
      this.state.set('error');
      return;
    }

    this.selectedFile.set(file);
    this.isPdf.set(pdf);
    this.result.set(null);
    this.errorMessage.set(null);
    this.previewSrc.set(null);

    // Show local preview for images; PDFs get a placeholder icon instead
    if (!pdf) {
      const reader = new FileReader();
      reader.onload = (e) => this.previewSrc.set(e.target?.result as string);
      reader.readAsDataURL(file);
    }

    this.processFile(file);
  }

  processFile(file: File): void {
    this.state.set('uploading');

    this.imageService.processImage(file).subscribe({
      next: (response) => {
        if (response.success) {
          this.result.set(response);
          this.state.set('done');
          this.activeTab.set('processed');
        } else {
          this.errorMessage.set(response.errorMessage ?? 'Processing failed.');
          this.state.set('error');
        }
      },
      error: (err) => {
        const msg = err?.error?.errorMessage ?? err?.message ?? 'Network error — is the backend running?';
        this.errorMessage.set(msg);
        this.state.set('error');
      }
    });

    // Simulate transition to "processing" after a short delay (upload → AI)
    setTimeout(() => {
      if (this.state() === 'uploading') this.state.set('processing');
    }, 800);
  }

  reset(): void {
    this.state.set('idle');
    this.result.set(null);
    this.previewSrc.set(null);
    this.selectedFile.set(null);
    this.isPdf.set(false);
    this.errorMessage.set(null);
    this.isDragging.set(false);
  }

  setTab(tab: 'original' | 'processed'): void {
    this.activeTab.set(tab);
  }

  async copyText(): Promise<void> {
    const text = this.result()?.extractedText;
    if (!text) return;
    try {
      await navigator.clipboard.writeText(text);
      this.copyLabel.set('Copied!');
      setTimeout(() => this.copyLabel.set('Copy'), 2000);
    } catch {
      this.copyLabel.set('Failed');
      setTimeout(() => this.copyLabel.set('Copy'), 2000);
    }
  }

  objectEntries(obj: Record<string, string> | null | undefined): [string, string][] {
    return obj ? Object.entries(obj) : [];
  }

  downloadText(): void {
    const text = this.result()?.extractedText;
    if (!text) return;
    const blob = new Blob([text], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'extracted-text.txt';
    a.click();
    URL.revokeObjectURL(url);
  }
}
