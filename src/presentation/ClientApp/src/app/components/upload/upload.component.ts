import { Component, signal, inject, ElementRef, viewChild, OnInit } from '@angular/core';
import { NgClass } from '@angular/common';
import { ImageAnalysisService } from '../../services/image-analysis.service';
import { ResultCardComponent } from '../result-card/result-card.component';

@Component({
  selector: 'app-upload',
  standalone: true,
  imports: [NgClass, ResultCardComponent],
  templateUrl: './upload.component.html',
  styleUrl: './upload.component.css'
})
export class UploadComponent implements OnInit {
  private readonly analysisService = inject(ImageAnalysisService);
  readonly fileInput = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');

  readonly state = this.analysisService.state;
  readonly result = this.analysisService.result;
  readonly errorMessage = this.analysisService.errorMessage;
  readonly isLoading = this.analysisService.isLoading;
  readonly hasResult = this.analysisService.hasResult;
  readonly hasError = this.analysisService.hasError;

  availableModels = signal<string[]>([]);
  selectedModel = signal('gemma4:e4b');
  isLoadingModels = signal(true);
  isDragging = signal(false);
  selectedFile = signal<File | null>(null);
  previewUrl = signal<string | null>(null);

  get hasVisionModels(): boolean { return this.availableModels().length > 0; }

  ngOnInit(): void {
    this.analysisService.getAvailableModels().subscribe({
      next: (models) => {
        const uniqueModels = [...new Set(models)].filter((x) => !!x?.trim());
        if (uniqueModels.length > 0) {
          this.availableModels.set(uniqueModels);
          this.selectedModel.set(uniqueModels[0]);
        } else {
          this.availableModels.set([]);
          this.selectedModel.set('');
        }
        this.isLoadingModels.set(false);
      },
      error: () => {
        this.availableModels.set([]);
        this.selectedModel.set('');
        this.isLoadingModels.set(false);
      }
    });
  }

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
    this.analysisService.clearAnalysis();
    this.fileInput().nativeElement.value = '';
  }

  onModelSelected(event: Event): void {
    const select = event.target as HTMLSelectElement;
    const model = select.value?.trim();
    if (model) {
      this.selectedModel.set(model);
    }
  }

  analyzeImage(): void {
    const file = this.selectedFile();
    if (!file || !this.selectedModel()) return;

    this.analysisService.analyzeImage(file, this.selectedModel());
  }

  formatFileSize(bytes: number): string {
    if (bytes < 1024) return `${bytes} B`;
    if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }

  private selectFile(file: File): void {
    this.selectedFile.set(file);
    this.analysisService.clearAnalysis();

    const reader = new FileReader();
    reader.onload = (e) => this.previewUrl.set(e.target?.result as string);
    reader.readAsDataURL(file);
  }
}
