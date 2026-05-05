import { useEffect, useRef } from 'react'
import { useTranslation } from 'react-i18next'

interface Props {
  value: string | null
  onChange: (dataUrl: string | null) => void
  error?: string
}

/**
 * Lightweight pointer/touch signature pad. No external deps.
 * Calls onChange with a base64 PNG data URL after each stroke, or null when cleared.
 */
export function SignaturePad({ value, onChange, error }: Props) {
  const { t } = useTranslation()
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const isDrawing = useRef(false)
  const hasContent = useRef(false)
  const lastPoint = useRef<{ x: number; y: number } | null>(null)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const dpr = window.devicePixelRatio || 1
    const rect = canvas.getBoundingClientRect()
    canvas.width = rect.width * dpr
    canvas.height = rect.height * dpr

    const ctx = canvas.getContext('2d')
    if (!ctx) return
    ctx.scale(dpr, dpr)
    ctx.lineWidth = 2
    ctx.lineCap = 'round'
    ctx.lineJoin = 'round'
    ctx.strokeStyle = '#0f172a'

    // Restore from value (e.g. when component remounts in StrictMode)
    if (value) {
      const img = new Image()
      img.onload = () => ctx.drawImage(img, 0, 0, rect.width, rect.height)
      img.src = value
      hasContent.current = true
    }
  }, [])

  const pointFromEvent = (e: PointerEvent | React.PointerEvent) => {
    const canvas = canvasRef.current!
    const rect = canvas.getBoundingClientRect()
    return { x: e.clientX - rect.left, y: e.clientY - rect.top }
  }

  const start = (e: React.PointerEvent) => {
    e.preventDefault()
    canvasRef.current?.setPointerCapture(e.pointerId)
    isDrawing.current = true
    lastPoint.current = pointFromEvent(e)
  }

  const move = (e: React.PointerEvent) => {
    if (!isDrawing.current) return
    e.preventDefault()
    const ctx = canvasRef.current?.getContext('2d')
    if (!ctx || !lastPoint.current) return
    const p = pointFromEvent(e)
    ctx.beginPath()
    ctx.moveTo(lastPoint.current.x, lastPoint.current.y)
    ctx.lineTo(p.x, p.y)
    ctx.stroke()
    lastPoint.current = p
    hasContent.current = true
  }

  const end = (e: React.PointerEvent) => {
    if (!isDrawing.current) return
    canvasRef.current?.releasePointerCapture(e.pointerId)
    isDrawing.current = false
    lastPoint.current = null
    if (hasContent.current && canvasRef.current) {
      onChange(canvasRef.current.toDataURL('image/png'))
    }
  }

  const clear = () => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return
    const dpr = window.devicePixelRatio || 1
    ctx.save()
    ctx.setTransform(1, 0, 0, 1, 0, 0)
    ctx.clearRect(0, 0, canvas.width, canvas.height)
    ctx.restore()
    ctx.scale(dpr, dpr)
    hasContent.current = false
    onChange(null)
  }

  return (
    <div>
      <div
        className={`relative bg-slate-50 border ${error ? 'border-rose-400' : 'border-slate-300'} rounded-md`}
        style={{ height: 140 }}
      >
        <canvas
          ref={canvasRef}
          className="w-full h-full touch-none rounded-md"
          onPointerDown={start}
          onPointerMove={move}
          onPointerUp={end}
          onPointerCancel={end}
          onPointerLeave={end}
        />
        {!value && (
          <span className="pointer-events-none absolute inset-0 flex items-center justify-center text-slate-400 text-sm select-none">
            ✍︎
          </span>
        )}
      </div>
      <div className="flex items-center justify-between mt-2 text-xs">
        <span className="text-slate-500">{t('register.waiver.signatureHelp')}</span>
        <button type="button" onClick={clear} className="text-rose-600 hover:underline">
          {t('register.waiver.signatureClear')}
        </button>
      </div>
      {error && <span className="block text-rose-600 text-xs mt-1">{error}</span>}
    </div>
  )
}
