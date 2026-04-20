import { Component, input } from '@angular/core';
import { DatePipe } from '@angular/common';
import { AnalysisResult } from '../../models/analysis-result.model';

@Component({
  selector: 'app-result-card',
  standalone: true,
  imports: [DatePipe],
  templateUrl: './result-card.component.html',
  styleUrl: './result-card.component.css'
})
export class ResultCardComponent {
  readonly result = input.required<AnalysisResult>();
}
