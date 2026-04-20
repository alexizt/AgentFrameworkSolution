import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { AnalysisResult } from '../models/analysis-result.model';

export type AnalysisState = 'idle' | 'loading' | 'success' | 'error';

@Injectable({ providedIn: 'root' })
export class ImageAnalysisService {
  private readonly http = inject(HttpClient);

  private readonly stateSignal = signal<AnalysisState>('idle');
  private readonly resultSignal = signal<AnalysisResult | null>(null);
  private readonly errorMessageSignal = signal<string | null>(null);
  private readonly availableModelsSignal = signal<string[]>([]);
  private readonly selectedModelSignal = signal('');
  private readonly isLoadingModelsSignal = signal(true);

  readonly state = this.stateSignal.asReadonly();
  readonly result = this.resultSignal.asReadonly();
  readonly errorMessage = this.errorMessageSignal.asReadonly();
  readonly availableModels = this.availableModelsSignal.asReadonly();
  readonly selectedModel = this.selectedModelSignal.asReadonly();
  readonly isLoadingModels = this.isLoadingModelsSignal.asReadonly();
  readonly isLoading = computed(() => this.state() === 'loading');
  readonly hasResult = computed(() => this.state() === 'success');
  readonly hasError = computed(() => this.state() === 'error');
  readonly hasVisionModels = computed(() => this.availableModels().length > 0);

  loadAvailableModels(): void {
    this.isLoadingModelsSignal.set(true);

    this.http.get<string[]>('/api/imageanalysis/models').subscribe({
      next: (models) => {
        const uniqueModels = [...new Set(models)].filter((x) => !!x?.trim());
        if (uniqueModels.length > 0) {
          this.availableModelsSignal.set(uniqueModels);
          this.selectedModelSignal.set(uniqueModels[0]);
        } else {
          this.availableModelsSignal.set([]);
          this.selectedModelSignal.set('');
        }

        this.isLoadingModelsSignal.set(false);
      },
      error: () => {
        this.availableModelsSignal.set([]);
        this.selectedModelSignal.set('');
        this.isLoadingModelsSignal.set(false);
      }
    });
  }

  selectModel(model: string): void {
    const selected = model.trim();
    if (selected) {
      this.selectedModelSignal.set(selected);
    }
  }

  analyzeImage(file: File, model: string, language: string = 'English'): void {
    this.stateSignal.set('loading');
    this.errorMessageSignal.set(null);
    this.resultSignal.set(null);

    const formData = new FormData();
    formData.append('file', file);
    formData.append('model', model);
    formData.append('language', language);

    this.http.post<AnalysisResult>('/api/imageanalysis', formData).subscribe({
      next: (data) => {
        this.resultSignal.set(data);
        this.stateSignal.set('success');
      },
      error: (err) => {
        const msg = err?.error?.error ?? 'Analysis failed. Please try again.';
        this.errorMessageSignal.set(msg);
        this.stateSignal.set('error');
      }
    });
  }

  clearAnalysis(): void {
    this.resultSignal.set(null);
    this.errorMessageSignal.set(null);
    this.stateSignal.set('idle');
  }
}
