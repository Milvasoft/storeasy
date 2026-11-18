import { Trash2 } from 'lucide-react'
import { AssetUpload } from '@/components/ui/AssetUpload'
import { SegmentedControl } from '@/components/ui/Tabs'
import { Button, Field, Input } from '@/components/ui/primitives'
import { AssetPurpose } from '@/lib/api/types'
import { useI18n } from '@/lib/i18n/I18nContext'
import { ColorField, NumberField, PanelSection, SelectField, SwitchRow } from './fields'
import { useStudioT } from './i18n'
import { classLabel } from './labels'
import { fromPercent, isLocaleTag, parseLocales, toPercent } from './spec'
import type { MockupClassDto, MockupLayerSpec, MockupSpec, MockupTextStyleSpec } from './types'
import type { TemplateDraft } from './useTemplateEditor'

// The editor's right panel: one form per part of the spec. Every change goes through `onSpec` (or `onDraft`) as a new
// object; the preview follows after the debounce.

type SpecChange = (change: (spec: MockupSpec) => MockupSpec) => void

const IMAGE_TYPES = ['.png', '.jpg', '.jpeg']
const WEIGHTS = [400, 500, 600, 700, 800]

export function TemplateProperties({
  draft,
  classes,
  onDraft,
}: {
  draft: TemplateDraft
  classes: MockupClassDto[]
  onDraft: (change: (draft: TemplateDraft) => TemplateDraft) => void
}) {
  const s = useStudioT()
  const { lang } = useI18n()
  const localeText = draft.Locales.join(', ')
  const bad = draft.Locales.find((locale) => !isLocaleTag(locale))
  return (
    <PanelSection title={s.props.template}>
      <Field label={s.create.name}>
        <Input value={draft.Name} maxLength={100} onChange={(event) => onDraft((d) => ({ ...d, Name: event.target.value }))} />
      </Field>
      <Field label={s.create.locales} hint={s.create.localesHint} error={bad ? s.create.localeInvalid.replace('{locale}', bad) : null}>
        <Input
          defaultValue={localeText}
          onBlur={(event) => {
            const locales = parseLocales(event.target.value)
            onDraft((d) => ({
              ...d,
              Locales: locales.length > 0 ? locales : d.Locales,
              PrimaryLocale: locales.includes(d.PrimaryLocale) ? d.PrimaryLocale : (locales[0] ?? d.PrimaryLocale),
            }))
          }}
        />
      </Field>
      <SelectField
        label={s.create.primary}
        value={draft.PrimaryLocale}
        options={draft.Locales.map((locale) => ({ value: locale, label: locale }))}
        onChange={(value) => onDraft((d) => ({ ...d, PrimaryLocale: value }))}
      />
      <SelectField
        label={s.props.canvasClass}
        value={draft.Spec.Canvas?.ClassKey ?? null}
        options={classes.map((item) => ({ value: item.ClassKey, label: `${classLabel(item, lang)} · ${item.Width}x${item.Height}` }))}
        onChange={(value) => onDraft((d) => ({ ...d, Spec: { ...d.Spec, Canvas: { ...d.Spec.Canvas, ClassKey: value } } }))}
      />
    </PanelSection>
  )
}

export function BackgroundProperties({ spec, onSpec }: { spec: MockupSpec; onSpec: SpecChange }) {
  const s = useStudioT()
  const background = spec.Background ?? { Kind: 'solid', Colors: ['#4636FF'], Angle: 90, AssetId: null }
  const colors = background.Colors ?? []
  const set = (patch: Partial<typeof background>) => onSpec((current) => ({ ...current, Background: { ...background, ...patch } }))
  const setColor = (index: number, value: string | null) => {
    const next = [...colors]
    next[index] = value ?? ''
    set({ Colors: next.filter(Boolean) })
  }
  return (
    <PanelSection title={s.props.background}>
      <SegmentedControl
        label={s.props.backgroundKind}
        value={background.Kind}
        options={[
          { value: 'solid', label: s.props.solid },
          { value: 'gradient', label: s.props.gradient },
          { value: 'image', label: s.props.image },
        ]}
        onChange={(kind) =>
          set({ Kind: kind, Colors: kind === 'gradient' && colors.length < 2 ? [colors[0] ?? '#4636FF', '#1B1464'] : colors })
        }
      />
      {background.Kind === 'image' ? (
        <>
          <p className="text-xs text-fg3">
            {background.AssetId ? s.props.imageSet.replace('{id}', String(background.AssetId)) : s.props.imageMissing}
          </p>
          <AssetUpload
            purpose={AssetPurpose.Graphic}
            accept={IMAGE_TYPES}
            label={s.props.imageUpload}
            hint={s.props.imageHint}
            onUploaded={(asset) => set({ AssetId: asset.Id })}
          />
        </>
      ) : null}
      <ColorField label={s.props.color1} value={colors[0]} onChange={(value) => setColor(0, value)} />
      {background.Kind === 'gradient' ? (
        <>
          <ColorField label={s.props.color2} value={colors[1]} onChange={(value) => setColor(1, value)} />
          <NumberField
            label={s.props.angle}
            suffix="°"
            value={background.Angle}
            min={0}
            max={360}
            onChange={(value) => set({ Angle: value })}
          />
        </>
      ) : null}
    </PanelSection>
  )
}

export function FrameProperties({ spec, onSpec }: { spec: MockupSpec; onSpec: SpecChange }) {
  const s = useStudioT()
  const frame = spec.Frame ?? {
    Kind: 'auto',
    Cutout: 'island',
    Color: '#1C1C22',
    Scale: 0.78,
    OffsetX: 0,
    OffsetY: 0.27,
    Rotation: 0,
    Shadow: true,
  }
  const set = (patch: Partial<typeof frame>) => onSpec((current) => ({ ...current, Frame: { ...frame, ...patch } }))
  return (
    <PanelSection title={s.props.frame}>
      <SelectField
        label={s.props.frameKind}
        value={frame.Kind}
        options={[
          { value: 'auto', label: s.props.frameAuto },
          { value: 'phone', label: s.enums.frame.phone },
          { value: 'tablet', label: s.enums.frame.tablet },
          { value: 'foldable', label: s.props.foldable },
          { value: 'desktop', label: s.enums.frame.desktop },
          { value: 'none', label: s.enums.frame.none },
        ]}
        onChange={(value) => set({ Kind: value })}
      />
      <SelectField
        label={s.props.cutout}
        value={frame.Cutout ?? 'none'}
        options={[
          { value: 'island', label: s.props.island },
          { value: 'notch', label: s.props.notch },
          { value: 'hole', label: s.props.hole },
          { value: 'none', label: s.props.noCutout },
        ]}
        onChange={(value) => set({ Cutout: value })}
      />
      <ColorField label={s.props.frameColor} value={frame.Color} onChange={(value) => set({ Color: value })} />
      <div className="grid grid-cols-2 gap-3">
        <NumberField
          label={s.props.scale}
          suffix="%"
          value={toPercent(frame.Scale)}
          min={10}
          max={150}
          onChange={(v) => set({ Scale: fromPercent(v) })}
        />
        <NumberField label={s.props.rotation} suffix="°" value={frame.Rotation} min={-45} max={45} onChange={(v) => set({ Rotation: v })} />
        <NumberField
          label={s.props.offsetX}
          suffix="%"
          value={toPercent(frame.OffsetX)}
          min={-100}
          max={100}
          onChange={(v) => set({ OffsetX: fromPercent(v) })}
        />
        <NumberField
          label={s.props.offsetY}
          suffix="%"
          value={toPercent(frame.OffsetY)}
          min={-100}
          max={100}
          onChange={(v) => set({ OffsetY: fromPercent(v) })}
        />
        <NumberField
          label={s.props.cornerRadius}
          suffix="%"
          value={toPercent(frame.CornerRadius)}
          min={0}
          max={50}
          step={0.5}
          onChange={(v) => set({ CornerRadius: v === 0 ? null : fromPercent(v) })}
        />
        <NumberField
          label={s.props.bezel}
          suffix="%"
          value={toPercent(frame.Bezel)}
          min={0}
          max={20}
          step={0.5}
          onChange={(v) => set({ Bezel: v === 0 ? null : fromPercent(v) })}
        />
      </div>
      <p className="text-xs text-fg3">{s.props.frameDefaultsHint}</p>
      <SwitchRow label={s.props.shadow} checked={frame.Shadow} onChange={(value) => set({ Shadow: value })} />
    </PanelSection>
  )
}

function TextStyleFields({
  title,
  style,
  onChange,
}: {
  title: string
  style: MockupTextStyleSpec
  onChange: (patch: Partial<MockupTextStyleSpec>) => void
}) {
  const s = useStudioT()
  return (
    <fieldset className="grid gap-3">
      <legend className="mb-2 text-[13px] font-semibold">{title}</legend>
      <div className="grid grid-cols-2 gap-3">
        <NumberField
          label={`${title} ${s.props.size}`}
          suffix="%"
          value={toPercent(style.Size)}
          min={1}
          max={30}
          step={0.1}
          onChange={(v) => onChange({ Size: fromPercent(v) })}
        />
        <SelectField
          label={`${title} ${s.props.weight}`}
          value={style.Weight}
          options={WEIGHTS.map((weight) => ({ value: weight, label: String(weight) }))}
          onChange={(value) => onChange({ Weight: value })}
        />
        <NumberField
          label={`${title} ${s.props.maxLines}`}
          value={style.MaxLines}
          min={1}
          max={6}
          onChange={(v) => onChange({ MaxLines: Math.round(v) })}
        />
        <NumberField
          label={`${title} ${s.props.lineHeight}`}
          value={style.LineHeight}
          min={0.8}
          max={2}
          step={0.05}
          onChange={(v) => onChange({ LineHeight: v })}
        />
        <NumberField
          label={`${title} ${s.props.minSize}`}
          suffix="%"
          value={toPercent(style.MinSize)}
          min={1}
          max={30}
          step={0.1}
          onChange={(v) => onChange({ MinSize: fromPercent(v) })}
        />
      </div>
      <SwitchRow label={`${title} ${s.props.autoFit}`} checked={style.AutoFit} onChange={(value) => onChange({ AutoFit: value })} />
      <ColorField label={`${title} ${s.props.ownColor}`} optional value={style.Color} onChange={(value) => onChange({ Color: value })} />
    </fieldset>
  )
}

export function TextProperties({ spec, onSpec }: { spec: MockupSpec; onSpec: SpecChange }) {
  const s = useStudioT()
  const text = spec.Text
  if (!text) {
    return null
  }
  const set = (patch: Partial<typeof text>) => onSpec((current) => ({ ...current, Text: { ...text, ...patch } }))
  return (
    <PanelSection title={s.props.text}>
      <SelectField
        label={s.props.font}
        value={text.Font}
        options={[
          { value: 'inter', label: 'Inter' },
          { value: 'noto-sans', label: 'Noto Sans' },
        ]}
        onChange={(value) => set({ Font: value })}
      />
      <ColorField label={s.props.textColor} value={text.Color} onChange={(value) => set({ Color: value })} />
      <SegmentedControl
        label={s.props.position}
        value={text.Position}
        options={[
          { value: 'top', label: s.props.top },
          { value: 'bottom', label: s.props.bottom },
        ]}
        onChange={(value) => set({ Position: value })}
      />
      <SegmentedControl
        label={s.props.alignment}
        value={text.Alignment}
        options={[
          { value: 'start', label: s.props.alignStart },
          { value: 'center', label: s.props.alignCenter },
          { value: 'end', label: s.props.alignEnd },
        ]}
        onChange={(value) => set({ Alignment: value })}
      />
      <div className="grid grid-cols-2 gap-3">
        <NumberField
          label={s.props.margin}
          suffix="%"
          value={toPercent(text.Margin)}
          min={0}
          max={40}
          step={0.5}
          onChange={(v) => set({ Margin: fromPercent(v) })}
        />
        <NumberField
          label={s.props.gap}
          suffix="%"
          value={toPercent(text.Gap)}
          min={0}
          max={20}
          step={0.5}
          onChange={(v) => set({ Gap: fromPercent(v) })}
        />
      </div>
      <SwitchRow label={s.props.uppercase} checked={text.Uppercase} onChange={(value) => set({ Uppercase: value })} />
      {text.Title ? (
        <TextStyleFields title={s.captions.title} style={text.Title} onChange={(patch) => set({ Title: { ...text.Title!, ...patch } })} />
      ) : null}
      {text.Subtitle ? (
        <TextStyleFields
          title={s.captions.subtitle}
          style={text.Subtitle}
          onChange={(patch) => set({ Subtitle: { ...text.Subtitle!, ...patch } })}
        />
      ) : null}
    </PanelSection>
  )
}

export function LayerProperties({
  layer,
  index,
  locales,
  onSpec,
  onRemoved,
}: {
  layer: MockupLayerSpec
  index: number
  locales: string[]
  onSpec: SpecChange
  onRemoved: () => void
}) {
  const s = useStudioT()
  const set = (patch: Partial<MockupLayerSpec>) =>
    onSpec((current) => ({ ...current, Layers: (current.Layers ?? []).map((item, i) => (i === index ? { ...item, ...patch } : item)) }))
  const remove = () => {
    onSpec((current) => ({ ...current, Layers: (current.Layers ?? []).filter((_, i) => i !== index) }))
    onRemoved()
  }
  const isBadge = layer.Type === 'badge'
  const texts = layer.Texts ?? {}
  return (
    <PanelSection
      title={isBadge ? s.layers.badge : s.layers.shape}
      actions={
        <Button size="sm" variant="ghost" onClick={remove}>
          <Trash2 aria-hidden />
          {s.layers.remove}
        </Button>
      }
    >
      {!isBadge ? (
        <SelectField
          label={s.props.shapeKind}
          value={layer.Shape ?? 'rect'}
          options={[
            { value: 'rect', label: s.props.rect },
            { value: 'round-rect', label: s.props.roundRect },
            { value: 'ellipse', label: s.props.ellipse },
          ]}
          onChange={(value) => set({ Shape: value })}
        />
      ) : null}
      <div className="grid grid-cols-2 gap-3">
        <NumberField label="X" suffix="%" value={toPercent(layer.X)} min={0} max={100} onChange={(v) => set({ X: fromPercent(v) })} />
        <NumberField label="Y" suffix="%" value={toPercent(layer.Y)} min={0} max={100} onChange={(v) => set({ Y: fromPercent(v) })} />
        {!isBadge ? (
          <>
            <NumberField
              label={s.props.width}
              suffix="%"
              value={toPercent(layer.Width)}
              min={0}
              max={100}
              onChange={(v) => set({ Width: fromPercent(v) })}
            />
            <NumberField
              label={s.props.height}
              suffix="%"
              value={toPercent(layer.Height)}
              min={0}
              max={100}
              onChange={(v) => set({ Height: fromPercent(v) })}
            />
            <NumberField
              label={s.props.radius}
              suffix="%"
              value={toPercent(layer.Radius)}
              min={0}
              max={50}
              step={0.5}
              onChange={(v) => set({ Radius: fromPercent(v) })}
            />
          </>
        ) : (
          <NumberField
            label={s.props.textSize}
            suffix="%"
            value={toPercent(layer.TextSize)}
            min={1}
            max={20}
            step={0.1}
            onChange={(v) => set({ TextSize: fromPercent(v) })}
          />
        )}
        <NumberField
          label={s.props.opacity}
          suffix="%"
          value={Math.round(layer.Opacity * 100)}
          min={0}
          max={100}
          onChange={(v) => set({ Opacity: v / 100 })}
        />
      </div>
      <ColorField label={s.props.fill} value={layer.Color} onChange={(value) => set({ Color: value })} />
      {isBadge ? (
        <>
          <ColorField label={s.props.textColor} value={layer.TextColor} onChange={(value) => set({ TextColor: value })} />
          {['*', ...locales].map((locale) => (
            <Field key={locale} label={locale === '*' ? s.props.badgeTextAll : s.props.badgeTextIn.replace('{locale}', locale)}>
              <Input
                value={texts[locale] ?? ''}
                maxLength={40}
                onChange={(event) => {
                  const next = { ...texts }
                  if (event.target.value) {
                    next[locale] = event.target.value
                  } else {
                    delete next[locale]
                  }
                  set({ Texts: next })
                }}
              />
            </Field>
          ))}
        </>
      ) : null}
      <SwitchRow label={s.props.above} checked={layer.Above} onChange={(value) => set({ Above: value })} />
    </PanelSection>
  )
}
