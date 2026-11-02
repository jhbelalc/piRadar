// Marco de recorte sobre la foto del recibo (pantalla CropEditor). El marco se guarda normalizado (0–1) sobre la
// imagen: se arrastran bordes y esquinas para cambiar su tamaño y el centro para moverlo. touch-action: none en el
// escenario evita que el gesto desplace la página.
const MIN = 0.1;

export function attach(stage, frame, initial) {
    let rect = { ...initial };
    let drag = null;

    const apply = () => {
        frame.style.left = `${rect.left * 100}%`;
        frame.style.top = `${rect.top * 100}%`;
        frame.style.width = `${(rect.right - rect.left) * 100}%`;
        frame.style.height = `${(rect.bottom - rect.top) * 100}%`;
    };

    const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));

    const onDown = (e) => {
        const handle = e.target.closest('[data-edge]');
        if (!handle) return;
        e.preventDefault();
        // Sin la captura el arrastre sigue funcionando mientras el dedo no salga de la foto.
        try { stage.setPointerCapture(e.pointerId); } catch { }
        const r = stage.getBoundingClientRect();
        drag = { id: e.pointerId, edge: handle.dataset.edge, x: e.clientX, y: e.clientY, w: r.width, h: r.height, start: { ...rect } };
    };

    const onMove = (e) => {
        if (!drag || e.pointerId !== drag.id) return;
        const dx = (e.clientX - drag.x) / drag.w, dy = (e.clientY - drag.y) / drag.h;
        const s = drag.start, edge = drag.edge;
        const next = { ...s };
        if (edge === 'move') {
            const w = s.right - s.left, h = s.bottom - s.top;
            next.left = clamp(s.left + dx, 0, 1 - w);
            next.top = clamp(s.top + dy, 0, 1 - h);
            next.right = next.left + w;
            next.bottom = next.top + h;
        } else {
            if (edge.includes('w')) next.left = clamp(s.left + dx, 0, s.right - MIN);
            if (edge.includes('e')) next.right = clamp(s.right + dx, s.left + MIN, 1);
            if (edge.includes('n')) next.top = clamp(s.top + dy, 0, s.bottom - MIN);
            if (edge.includes('s')) next.bottom = clamp(s.bottom + dy, s.top + MIN, 1);
        }
        rect = next;
        apply();
    };

    const onUp = (e) => {
        if (drag && e.pointerId === drag.id) drag = null;
    };

    stage.addEventListener('pointerdown', onDown);
    stage.addEventListener('pointermove', onMove);
    stage.addEventListener('pointerup', onUp);
    stage.addEventListener('pointercancel', onUp);
    apply();

    return {
        get: () => [rect.left, rect.top, rect.right, rect.bottom],
        set: (left, top, right, bottom) => { rect = { left, top, right, bottom }; apply(); },
        detach: () => {
            stage.removeEventListener('pointerdown', onDown);
            stage.removeEventListener('pointermove', onMove);
            stage.removeEventListener('pointerup', onUp);
            stage.removeEventListener('pointercancel', onUp);
        },
    };
}
