import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { AnalysisResult } from '../models/analysis-result.model';

@Injectable({ providedIn: 'root' })
export class ImageAnalysisService {
  private readonly http = inject(HttpClient);

  analyzeImage(file: File): Observable<AnalysisResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<AnalysisResult>('/api/imageanalysis', formData);
  }
}
