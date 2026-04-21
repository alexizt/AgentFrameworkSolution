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
  readonly availableModels = this.analysisService.availableModels;
  readonly selectedModel = this.analysisService.selectedModel;
  readonly availableRoles = this.analysisService.availableRoles;
  readonly isLoadingModels = this.analysisService.isLoadingModels;
  readonly isLoadingRoles = this.analysisService.isLoadingRoles;
  readonly isLoadingDropdowns = this.analysisService.isLoadingDropdowns;
  readonly isLoading = this.analysisService.isLoading;
  readonly hasResult = this.analysisService.hasResult;
  readonly hasError = this.analysisService.hasError;
  readonly hasVisionModels = this.analysisService.hasVisionModels;

  readonly supportedLanguages = ['English', 'Spanish', 'Italian', 'French', 'German'];

  isDragging = signal(false);
  selectedFile = signal<File | null>(null);
  previewUrl = signal<string | null>(null);
  selectedLanguage = signal('English');
  selectedRole = signal('');

  ngOnInit(): void {
    this.analysisService.loadAvailableModels();
    this.analysisService.loadAvailableRoles();
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
    this.analysisService.selectModel(select.value ?? '');
  }

  onLanguageSelected(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedLanguage.set(select.value ?? 'English');
  }

  onRoleSelected(event: Event): void {
    const select = event.target as HTMLSelectElement;
    this.selectedRole.set(select.value ?? '');
  }

  analyzeImage(): void {
    const file = this.selectedFile();
    if (!file || !this.selectedModel() || !this.selectedRole()) return;

    this.analysisService.analyzeImage(file, this.selectedModel(), this.selectedRole(), this.selectedLanguage());
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
