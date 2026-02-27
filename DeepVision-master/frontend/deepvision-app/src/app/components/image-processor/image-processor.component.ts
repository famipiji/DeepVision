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
  lightboxOpen = signal(false);
  zoomScale   = signal(1);
  panX        = signal(0);
  panY        = signal(0);
  isPanning   = signal(false);

  private _panStartX = 0;
  private _panStartY = 0;
  private _panOriginX = 0;
  private _panOriginY = 0;

  // ── Computed ─────────────────────────────────────────────────────────────
  isIdle = computed(() => this.state() === 'idle');
  isLoading = computed(() => this.state() === 'uploading' || this.state() === 'processing');
  isDone = computed(() => this.state() === 'done');
  isError = computed(() => this.state() === 'error');

  statusLabel = computed(() => {
    const kind = this.isPdf() ? 'PDF' : 'image';
    switch (this.state()) {
      case 'uploading':   return `Uploading ${kind}…`;
      case 'processing':  return 'Ai is extracting text…';
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

  lightboxTransform = computed(() =>
    `translate(${this.panX()}px, ${this.panY()}px) scale(${this.zoomScale()})`
  );

  lightboxCursor = computed(() => {
    if (this.zoomScale() <= 1) return 'zoom-in';
    return this.isPanning() ? 'grabbing' : 'grab';
  });

  zoomPercent = computed(() => Math.round(this.zoomScale() * 100));

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

  openLightbox(): void {
    this.lightboxOpen.set(true);
    this._resetZoom();
  }

  closeLightbox(): void {
    this.lightboxOpen.set(false);
    this._resetZoom();
  }

  private _resetZoom(): void {
    this.zoomScale.set(1);
    this.panX.set(0);
    this.panY.set(0);
  }

  onLightboxImageClick(event: MouseEvent): void {
    event.stopPropagation();
    if (this.zoomScale() !== 1) {
      this._resetZoom();
    } else {
      this.zoomScale.set(2.5);
    }
  }

  onLightboxWheel(event: WheelEvent): void {
    event.preventDefault();
    event.stopPropagation();
    const factor = event.deltaY < 0 ? 1.15 : 1 / 1.15;
    const next = Math.min(5, Math.max(1, this.zoomScale() * factor));
    this.zoomScale.set(next);
    if (next <= 1) { this.panX.set(0); this.panY.set(0); }
  }

  onPanStart(event: MouseEvent): void {
    if (this.zoomScale() <= 1) return;
    event.preventDefault();
    this.isPanning.set(true);
    this._panStartX  = event.clientX;
    this._panStartY  = event.clientY;
    this._panOriginX = this.panX();
    this._panOriginY = this.panY();
  }

  onPanMove(event: MouseEvent): void {
    if (!this.isPanning()) return;
    this.panX.set(this._panOriginX + event.clientX - this._panStartX);
    this.panY.set(this._panOriginY + event.clientY - this._panStartY);
  }

  onPanEnd(): void {
    this.isPanning.set(false);
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
