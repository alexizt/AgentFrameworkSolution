import { Component, signal, inject, ElementRef, viewChild } from '@angular/core';
import { NgClass } from '@angular/common';
import { ImageAnalysisService } from '../../services/image-analysis.service';
import { AnalysisResult } from '../../models/analysis-result.model';
import { ResultCardComponent } from '../result-card/result-card.component';

type AnalysisState = 'idle' | 'loading' | 'success' | 'error';

@Component({
  selector: 'app-upload',
  standalone: true,
  imports: [NgClass, ResultCardComponent],
  templateUrl: './upload.component.html',
  styleUrl: './upload.component.css'
})
export class UploadComponent {
  private readonly analysisService = inject(ImageAnalysisService);
  readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');

  isDragging = signal(false);
  selectedFile = signal<File | null>(null);
  previewUrl = signal<string | null>(null);
  state = signal<AnalysisState>('idle');
  result = signal<AnalysisResult | null>(null);
  errorMessage = signal<string | null>(null);

  get isLoading(): boolean { return this.state() === 'loading'; }
  get hasResult(): boolean { return this.state() === 'success'; }
  get hasError(): boolean { return this.state() === 'error'; }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(true);
  }

  onDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragging.set(false);
    const file = event.dataTransfer?.files[0];
    if (file) this.selectFile(file);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.selectFile(file);
  }

  openFilePicker(): void {
    this.fileInput().nativeElement.click();
  }

  clearSelection(): void {
    this.selectedFile.set(null);
    this.previewUrl.set(null);
    this.result.set(null);
    this.errorMessage.set(null);
    this.state.set('idle');
    this.fileInput().nativeElement.value = '';
  }

  analyzeImage(): void {
    const file = this.selectedFile();
    if (!file) return;

    this.state.set('loading');
    this.errorMessage.set(null);
    this.result.set(null);

    this.analysisService.analyzeImage(file).subscribe({
      next: (data) => {
        this.result.set(data);
        this.state.set('success');
      },
      error: (err) => {
        const msg = err?.error?.error ?? 'Analysis failed. Please try again.';
        this.errorMessage.set(msg);
        this.state.set('error');
      }
    });
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  private selectFile(file: File): void {
    this.selectedFile.set(file);
    this.result.set(null);
    this.errorMessage.set(null);
    this.state.set('idle');

    const reader = new FileReader();
    reader.onload = (e) => this.previewUrl.set(e.target?.result as string);
    reader.readAsDataURL(file);
  }
}
