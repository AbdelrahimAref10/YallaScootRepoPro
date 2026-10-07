import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';
import { ReportDefinition, ReportScopeName, reportsBaseRoute, reportsFor } from './reports.config';

interface ReportGroup {
  groupKey: string;
  reports: ReportDefinition[];
}

@Component({
  selector: 'app-reports',
  standalone: true,
  imports: [CommonModule, RouterModule, TranslatePipe],
  templateUrl: './reports.component.html',
  styleUrls: ['./reports.component.css', './report-page-shared.css']
})
export class ReportsComponent {
  private readonly route = inject(ActivatedRoute);

  readonly scope: ReportScopeName = this.route.snapshot.data['scope'] === 'merchant' ? 'merchant' : 'admin';
  readonly baseRoute = reportsBaseRoute(this.scope);
  readonly groups: ReportGroup[] = reportsFor(this.scope).reduce<ReportGroup[]>((groups, report) => {
    const group = groups.find(g => g.groupKey === report.groupKey);
    if (group) group.reports.push(report);
    else groups.push({ groupKey: report.groupKey, reports: [report] });
    return groups;
  }, []);
}
