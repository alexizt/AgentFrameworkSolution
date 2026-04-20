export interface AnalysisResult {
  id: string;
  fileName: string;
  summary: string;
  insights: string[];
  tags: string[];
  analyzedAt: string;
}
