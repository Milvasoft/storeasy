import {
  PreReviewArea,
  PreReviewSeverity,
  PreReviewStatus,
  PreReviewVerdict,
  StoreKind,
  type AiModelOptionDto,
  type PreReviewDto,
  type PreReviewListDto,
} from '@/lib/api/types'
import { make } from './factories'

// The AI pre-review fake data: the models of the AI settings and a report with a finding of each severity.

export function aiModels(): AiModelOptionDto[] {
  return [
    { Id: 'claude-haiku-5-5', Name: 'Claude Haiku 5.5', InputUsdPerMTok: 0.1, OutputUsdPerMTok: 0.5 },
    { Id: 'claude-sonnet-5-5', Name: 'Claude Sonnet 5.5', InputUsdPerMTok: 2, OutputUsdPerMTok: 10 },
    { Id: 'claude-opus-5-5', Name: 'Claude Opus 5.5', InputUsdPerMTok: 4, OutputUsdPerMTok: 20 },
  ]
}

export function preReview(overrides: Partial<PreReviewDto> = {}): PreReviewDto {
  return make<PreReviewDto>(
    {
      Id: 9001,
      AppId: 101,
      StoreAppId: 201,
      Store: StoreKind.Apple,
      VersionString: '2.4',
      Status: PreReviewStatus.Succeeded,
      JobId: 48400,
      Model: 'claude-sonnet-5-5',
      Language: 'tr-TR',
      Verdict: PreReviewVerdict.AtRisk,
      ApprovalLikelihood: 62,
      Summary: 'Kayıt büyük ölçüde hazır, ancak demo hesabı eksik.',
      Findings: [
        {
          Severity: PreReviewSeverity.Blocker,
          Area: PreReviewArea.Functionality,
          Guideline: 'App Store Review Guideline 2.1',
          Title: 'Demo hesabı yok',
          Evidence: 'İnceleme bilgilerinde giriş gerekiyor ama demo hesabı yok.',
          Fix: 'İnceleme bilgilerine çalışan bir demo hesabı ekle.',
        },
        {
          Severity: PreReviewSeverity.Major,
          Area: PreReviewArea.Metadata,
          Guideline: 'App Store Review Guideline 2.3.7',
          Title: 'Altyazıda fiyat bilgisi',
          Evidence: 'tr altyazı: "Ücretsiz rota planlayıcı"',
          Fix: 'Fiyat ifadesini altyazıdan çıkar.',
        },
        {
          Severity: PreReviewSeverity.Minor,
          Area: PreReviewArea.Screenshots,
          Guideline: 'App Store Review Guideline 2.3.3',
          Title: 'Ekran görüntüsünde cihaz çerçevesi eski',
          Evidence: 'APP_IPHONE_67, 3. görsel',
          Fix: 'Güncel cihaz çerçevesi kullan.',
        },
      ],
      RuntimeChecks: ['Konum izni reddedildiğinde uygulamanın çökmediğini kontrol et.'],
      PositiveNotes: ['Gizlilik politikası adresi dolu.'],
      ImageCount: 6,
      InputTokens: 18200,
      OutputTokens: 2100,
      CacheReadInputTokens: 0,
      CacheCreationInputTokens: 3000,
      EstimatedCostUsd: 0.0574,
      ErrorKey: null,
      ErrorMessage: null,
      CreatedDate: '2026-11-19T10:00:00Z',
      CreatedByUserName: 'deniz@kuzeylab.dev',
      CompletedDate: '2026-11-19T10:00:40Z',
    },
    overrides,
  )
}

/** The history row of a pre-review. */
export function preReviewRow(report: PreReviewDto): PreReviewListDto {
  const findings = report.Findings ?? []
  return {
    Id: report.Id,
    Store: report.Store,
    VersionString: report.VersionString,
    Status: report.Status,
    Verdict: report.Verdict,
    ApprovalLikelihood: report.ApprovalLikelihood,
    Model: report.Model,
    BlockerCount: findings.filter((finding) => finding.Severity === PreReviewSeverity.Blocker).length,
    MajorCount: findings.filter((finding) => finding.Severity === PreReviewSeverity.Major).length,
    MinorCount: findings.filter((finding) => finding.Severity === PreReviewSeverity.Minor).length,
    EstimatedCostUsd: report.EstimatedCostUsd,
    CreatedDate: report.CreatedDate,
    CreatedByUserName: report.CreatedByUserName,
  }
}
