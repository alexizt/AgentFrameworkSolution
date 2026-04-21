export interface AnalysisResult {
  id: string;
  fileName: string;
  summary: string;
  insights: string[];
  tags: string[];
  language: string;
  role: string;
  analyzedAt: string;
}
