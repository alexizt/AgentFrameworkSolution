import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AnalysisResult } from '../models/analysis-result.model';

@Injectable({ providedIn: 'root' })
export class ImageAnalysisService {
  private readonly http = inject(HttpClient);

  analyzeImage(file: File, model: string): Observable<AnalysisResult> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('model', model);
    return this.http.post<AnalysisResult>('/api/imageanalysis', formData);
  }

  getAvailableModels(): Observable<string[]> {
    return this.http.get<string[]>('/api/imageanalysis/models');
  }
}
