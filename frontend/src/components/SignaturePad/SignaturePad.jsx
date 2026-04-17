import { useCallback, useEffect, useRef, useState } from 'react';
import { Eraser, Upload, PenLine } from 'lucide-react';
import styles from './SignaturePad.module.css';

/**
 * Componente `SignaturePad` reutilizable.
 *
 * Dos modos accesibles por tabs:
 *   - Dibujar: canvas con trazo negro (grosor 2px) sobre fondo blanco.
 *     Soporta mouse y touch. Al soltar, emite `onChange(dataURL PNG)`.
 *   - Subir archivo: <input type="file"> PNG/JPEG. El archivo se carga en
 *     un `<img>` auxiliar, se dibuja sobre un canvas offscreen con la misma
 *     relación de aspecto (contained + fondo blanco) y se convierte a
 *     dataURL PNG. Si la imagen excede las dimensiones, se redimensiona.
 *
 * Props:
 *   - value:    string|null   dataURL PNG inicial (puede cambiar desde afuera).
 *   - onChange: (dataUrl|null) => void
 *   - width:    number        ancho en px (default 400)
 *   - height:   number        alto en px (default 150)
 *   - disabled: boolean       si true, bloquea interacción.
 */
export default function SignaturePad({
  value = null,
  onChange,
  width = 400,
  height = 150,
  disabled = false,
}) {
  const [tab, setTab] = useState('draw'); // 'draw' | 'upload'
  const canvasRef = useRef(null);
  const fileInputRef = useRef(null);
  const drawingRef = useRef(false);
  const lastPointRef = useRef(null);

  // Sincroniza el canvas con la prop `value` (evita bucles: solo redibuja
  // cuando cambia el valor externo distinto del último dataURL emitido).
  const lastEmittedRef = useRef(value || null);

  const paintValueIntoCanvas = useCallback(
    (dataUrl) => {
      const canvas = canvasRef.current;
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      ctx.fillStyle = '#ffffff';
      ctx.fillRect(0, 0, canvas.width, canvas.height);
      if (!dataUrl) return;
      const img = new Image();
      img.onload = () => {
        // Centrar contenedor manteniendo aspect ratio.
        const iw = img.width;
        const ih = img.height;
        const scale = Math.min(canvas.width / iw, canvas.height / ih);
        const dw = iw * scale;
        const dh = ih * scale;
        const dx = (canvas.width - dw) / 2;
        const dy = (canvas.height - dh) / 2;
        ctx.drawImage(img, dx, dy, dw, dh);
      };
      img.src = dataUrl;
    },
    [],
  );

  // Inicialización: fondo blanco al montar / cuando cambian dimensiones.
  useEffect(() => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.lineCap = 'round';
    ctx.lineJoin = 'round';
    ctx.lineWidth = 2;
    ctx.strokeStyle = '#000000';
    if (value) {
      paintValueIntoCanvas(value);
      lastEmittedRef.current = value;
    }
    // El efecto de primera carga solo debe correr al montar.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [width, height]);

  // Si `value` cambia desde afuera (ej. reset del form), repinta.
  useEffect(() => {
    if (value !== lastEmittedRef.current) {
      paintValueIntoCanvas(value);
      lastEmittedRef.current = value;
    }
  }, [value, paintValueIntoCanvas]);

  const getCanvasCoords = (event) => {
    const canvas = canvasRef.current;
    if (!canvas) return { x: 0, y: 0 };
    const rect = canvas.getBoundingClientRect();
    const scaleX = canvas.width / rect.width;
    const scaleY = canvas.height / rect.height;
    const source = event.touches ? event.touches[0] : event;
    return {
      x: (source.clientX - rect.left) * scaleX,
      y: (source.clientY - rect.top) * scaleY,
    };
  };

  const startStroke = (event) => {
    if (disabled) return;
    event.preventDefault();
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    drawingRef.current = true;
    const { x, y } = getCanvasCoords(event);
    lastPointRef.current = { x, y };
    ctx.beginPath();
    ctx.moveTo(x, y);
    // Trazar un punto al simplemente hacer click sin mover.
    ctx.lineTo(x + 0.01, y + 0.01);
    ctx.stroke();
  };

  const continueStroke = (event) => {
    if (!drawingRef.current || disabled) return;
    event.preventDefault();
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    const { x, y } = getCanvasCoords(event);
    ctx.beginPath();
    const last = lastPointRef.current || { x, y };
    ctx.moveTo(last.x, last.y);
    ctx.lineTo(x, y);
    ctx.stroke();
    lastPointRef.current = { x, y };
  };

  const endStroke = () => {
    if (!drawingRef.current) return;
    drawingRef.current = false;
    lastPointRef.current = null;
    const canvas = canvasRef.current;
    if (!canvas) return;
    const dataUrl = canvas.toDataURL('image/png');
    lastEmittedRef.current = dataUrl;
    onChange?.(dataUrl);
  };

  const handleClear = () => {
    const canvas = canvasRef.current;
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    ctx.fillStyle = '#ffffff';
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    lastEmittedRef.current = null;
    onChange?.(null);
  };

  const handleFileChange = async (event) => {
    const file = event.target.files?.[0];
    if (!file) return;
    try {
      const dataUrl = await fileToResizedPngDataUrl(file, width, height);
      lastEmittedRef.current = dataUrl;
      // También pintamos sobre el canvas por si el usuario vuelve a "Dibujar".
      paintValueIntoCanvas(dataUrl);
      onChange?.(dataUrl);
    } catch {
      // Sin lib de toast aquí; el caller puede enterarse porque onChange no se dispara.
      // Los consumidores (Modal de capacitación) ya validan presencia de firma.
    } finally {
      if (fileInputRef.current) fileInputRef.current.value = '';
    }
  };

  const triggerUpload = () => {
    if (disabled) return;
    fileInputRef.current?.click();
  };

  return (
    <div className={styles.root}>
      {/* Tabs */}
      <div className={styles.tabs} role="tablist">
        <button
          type="button"
          role="tab"
          className={`${styles.tab} ${tab === 'draw' ? styles.tabActive : ''}`}
          onClick={() => setTab('draw')}
          disabled={disabled}
          aria-selected={tab === 'draw'}
        >
          <PenLine width={14} height={14} style={{ verticalAlign: 'middle', marginRight: 4 }} />
          Dibujar
        </button>
        <button
          type="button"
          role="tab"
          className={`${styles.tab} ${tab === 'upload' ? styles.tabActive : ''}`}
          onClick={() => setTab('upload')}
          disabled={disabled}
          aria-selected={tab === 'upload'}
        >
          <Upload width={14} height={14} style={{ verticalAlign: 'middle', marginRight: 4 }} />
          Subir archivo
        </button>
      </div>

      {tab === 'draw' ? (
        <div className={styles.canvasWrapper}>
          <canvas
            ref={canvasRef}
            className={`${styles.canvas} ${disabled ? styles.canvasDisabled : ''}`}
            width={width}
            height={height}
            onMouseDown={startStroke}
            onMouseMove={continueStroke}
            onMouseUp={endStroke}
            onMouseLeave={endStroke}
            onTouchStart={startStroke}
            onTouchMove={continueStroke}
            onTouchEnd={endStroke}
            onTouchCancel={endStroke}
          />
          <div className={styles.actions}>
            <button
              type="button"
              className="btn btn--secondary btn--sm"
              onClick={handleClear}
              disabled={disabled}
            >
              <Eraser width={14} height={14} />
              <span>Limpiar</span>
            </button>
          </div>
          <span className={styles.hint}>
            Dibuja tu firma con el mouse o el dedo en pantalla táctil.
          </span>
        </div>
      ) : (
        <div className={styles.uploadArea}>
          <button
            type="button"
            className="btn btn--secondary btn--sm"
            onClick={triggerUpload}
            disabled={disabled}
          >
            <Upload width={14} height={14} />
            <span>Seleccionar archivo</span>
          </button>
          <input
            ref={fileInputRef}
            type="file"
            accept="image/png,image/jpeg"
            className={styles.hiddenInput}
            onChange={handleFileChange}
          />
          {value ? (
            <img
              src={value}
              alt="Vista previa de firma"
              className={styles.preview}
              style={{ width, height }}
            />
          ) : (
            <span className={styles.hint}>
              Admite PNG o JPG. Se ajustará a {width}×{height}px.
            </span>
          )}
        </div>
      )}
    </div>
  );
}

/**
 * Lee un File de imagen, lo pinta en un canvas offscreen del tamaño objetivo
 * con fondo blanco y "contain", y retorna el dataURL PNG.
 */
function fileToResizedPngDataUrl(file, targetWidth, targetHeight) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onerror = () => reject(new Error('No se pudo leer el archivo.'));
    reader.onload = () => {
      const img = new Image();
      img.onerror = () => reject(new Error('Imagen inválida.'));
      img.onload = () => {
        const canvas = document.createElement('canvas');
        canvas.width = targetWidth;
        canvas.height = targetHeight;
        const ctx = canvas.getContext('2d');
        ctx.fillStyle = '#ffffff';
        ctx.fillRect(0, 0, targetWidth, targetHeight);

        const scale = Math.min(targetWidth / img.width, targetHeight / img.height);
        const dw = img.width * scale;
        const dh = img.height * scale;
        const dx = (targetWidth - dw) / 2;
        const dy = (targetHeight - dh) / 2;
        ctx.drawImage(img, dx, dy, dw, dh);
        resolve(canvas.toDataURL('image/png'));
      };
      img.src = reader.result;
    };
    reader.readAsDataURL(file);
  });
}
