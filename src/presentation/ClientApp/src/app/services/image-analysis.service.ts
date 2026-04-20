import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AnalysisResult } from '../models/analysis-result.model';

export type AnalysisState = 'idle' | 'loading' | 'success' | 'error';

@Injectable({ providedIn: 'root' })
export class ImageAnalysisService {
  private readonly http = inject(HttpClient);

  private readonly stateSignal = signal<AnalysisState>('idle');
  private readonly resultSignal = signal<AnalysisResult | null>(null);
  private readonly errorMessageSignal = signal<string | null>(null);

  readonly state = this.stateSignal.asReadonly();
  readonly result = this.resultSignal.asReadonly();
  readonly errorMessage = this.errorMessageSignal.asReadonly();
  readonly isLoading = computed(() => this.state() === 'loading');
  readonly hasResult = computed(() => this.state() === 'success');
  readonly hasError = computed(() => this.state() === 'error');

  analyzeImage(file: File, model: string): void {
    this.stateSignal.set('loading');
    this.errorMessageSignal.set(null);
    this.resultSignal.set(null);

    const formData = new FormData();
    formData.append('file', file);
    formData.append('model', model);

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

  getAvailableModels(): Observable<string[]> {
    return this.http.get<string[]>('/api/imageanalysis/models');
  }

  clearAnalysis(): void {
    this.resultSignal.set(null);
    this.errorMessageSignal.set(null);
    this.stateSignal.set('idle');
  }
}
