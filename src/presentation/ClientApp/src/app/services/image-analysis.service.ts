import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, retry, timer } from 'rxjs';
import { AnalysisResult } from '../models/analysis-result.model';

export type AnalysisState = 'idle' | 'loading' | 'success' | 'error';

@Injectable({ providedIn: 'root' })
export class ImageAnalysisService {
  private readonly http = inject(HttpClient);
  private readonly modelsLoadRetryCount = 8;
  private readonly modelsLoadRetryBaseDelayMs = 750;

  private readonly stateSignal = signal<AnalysisState>('idle');
  private readonly resultSignal = signal<AnalysisResult | null>(null);
  private readonly errorMessageSignal = signal<string | null>(null);
  private readonly availableModelsSignal = signal<string[]>([]);
  private readonly selectedModelSignal = signal('');
  private readonly isLoadingModelsSignal = signal(true);
  private readonly availableRolesSignal = signal<string[]>([]);
  private readonly selectedRoleSignal = signal('');
  private readonly isLoadingRolesSignal = signal(true);

  readonly state = this.stateSignal.asReadonly();
  readonly result = this.resultSignal.asReadonly();
  readonly errorMessage = this.errorMessageSignal.asReadonly();
  readonly availableModels = this.availableModelsSignal.asReadonly();
  readonly selectedModel = this.selectedModelSignal.asReadonly();
  readonly isLoadingModels = this.isLoadingModelsSignal.asReadonly();
  readonly availableRoles = this.availableRolesSignal.asReadonly();
  readonly selectedRole = this.selectedRoleSignal.asReadonly();
  readonly isLoadingRoles = this.isLoadingRolesSignal.asReadonly();
  readonly isLoadingDropdowns = computed(() => this.isLoadingModels() || this.isLoadingRoles());
  readonly isLoading = computed(() => this.state() === 'loading');
  readonly hasResult = computed(() => this.state() === 'success');
  readonly hasError = computed(() => this.state() === 'error');
  readonly hasVisionModels = computed(() => this.availableModels().length > 0);

  loadAvailableModels(): void {
    this.isLoadingModelsSignal.set(true);

    this.getWithRetry('/api/imageanalysis/models')
      .subscribe({
        next: (models) => {
          const uniqueModels = this.getUniqueNonEmptyValues(models);
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

  loadAvailableRoles(): void {
    this.isLoadingRolesSignal.set(true);

    this.getWithRetry('/api/imageanalysis/roles')
      .subscribe({
        next: (roles) => {
          const uniqueRoles = this.getUniqueNonEmptyValues(roles);
          this.availableRolesSignal.set(uniqueRoles);
          if (uniqueRoles.length > 0) {
            this.selectedRoleSignal.set(uniqueRoles[0]);
          } else {
            this.selectedRoleSignal.set('');
          }
          this.isLoadingRolesSignal.set(false);
        },
        error: () => {
          this.availableRolesSignal.set([]);
          this.selectedRoleSignal.set('');
          this.isLoadingRolesSignal.set(false);
        }
      });
  }

  private getWithRetry(url: string): Observable<string[]> {
    return this.http.get<string[]>(url).pipe(
      retry({
        count: this.modelsLoadRetryCount,
        delay: (error, retryCount) => {
          if (!this.shouldRetryLoadingModels(error)) {
            throw error;
          }

          return timer(this.modelsLoadRetryBaseDelayMs * retryCount);
        }
      })
    );
  }

  private getUniqueNonEmptyValues(values: string[]): string[] {
    return [...new Set(values)]
      .map((value) => value?.trim())
      .filter((value): value is string => !!value);
  }

  private shouldRetryLoadingModels(error: unknown): boolean {
    const status = (error as { status?: number })?.status;
    return status === 0 || (typeof status === 'number' && status >= 500);
  }

  selectModel(model: string): void {
    const selected = model.trim();
    if (selected) {
      this.selectedModelSignal.set(selected);
    }
  }

  selectRole(role: string): void {
    const selected = role.trim();
    if (selected) {
      this.selectedRoleSignal.set(selected);
    }
  }

  analyzeImage(file: File, model: string, role: string, language: string = 'English'): void {
    this.stateSignal.set('loading');
    this.errorMessageSignal.set(null);
    this.resultSignal.set(null);

    const formData = new FormData();
    formData.append('file', file);
    formData.append('model', model);
    formData.append('language', language);
    formData.append('role', role);

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
