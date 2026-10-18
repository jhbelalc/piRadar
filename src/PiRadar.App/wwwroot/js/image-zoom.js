// Zoom y desplazamiento de una imagen dentro de un contenedor (visor de la foto del recibo).
// Pellizcar amplía (1×–5×), arrastrar recorre, doble toque alterna 1× / 2,5×. El viewport de la app bloquea el zoom
// de la página (user-scalable=no); aquí se transforma solo la imagen, con touch-action: none en el contenedor.
const MIN = 1, MAX = 5, DOUBLE = 2.5;

export function attach(container, img) {
    let scale = 1, x = 0, y = 0;
    const pointers = new Map();
    let pinch = null, pan = null, lastTap = 0;

    const apply = () => { img.style.transform = `translate(${x}px, ${y}px) scale(${scale})`; };

    // La imagen está centrada y ajustada (object-fit: contain): no deja asomar fondo de más al ampliarla.
    const clamp = () => {
        const r = container.getBoundingClientRect();
        const w = img.offsetWidth * scale, h = img.offsetHeight * scale;
        const maxX = Math.max(0, (w - r.width) / 2), maxY = Math.max(0, (h - r.height) / 2);
        x = Math.min(maxX, Math.max(-maxX, x));
        y = Math.min(maxY, Math.max(-maxY, y));
    };

    // Amplía manteniendo fijo el punto (cx, cy) de la pantalla.
    const zoomAt = (next, cx, cy) => {
        next = Math.min(MAX, Math.max(MIN, next));
        const r = container.getBoundingClientRect();
        const ox = cx - (r.left + r.width / 2), oy = cy - (r.top + r.height / 2);
        x = ox - (ox - x) * (next / scale);
        y = oy - (oy - y) * (next / scale);
        scale = next;
        if (scale === MIN) { x = 0; y = 0; }
        clamp();
        apply();
    };

    const distance = () => {
        const [a, b] = [...pointers.values()];
        return Math.hypot(a.x - b.x, a.y - b.y);
    };
    const middle = () => {
        const [a, b] = [...pointers.values()];
        return { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 };
    };

    const down = e => {
        if (e.target.closest('button')) return; // la X de cerrar recibe su toque normal
        try { container.setPointerCapture(e.pointerId); } catch { /* puntero sin captura posible */ }
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (pointers.size === 2) {
            pinch = { distance: distance(), scale };
            pan = null;
        } else if (pointers.size === 1) {
            pan = { x: e.clientX - x, y: e.clientY - y };
            const now = Date.now();
            if (now - lastTap < 300) {
                zoomAt(scale > 1 ? 1 : DOUBLE, e.clientX, e.clientY);
                lastTap = 0;
            } else {
                lastTap = now;
            }
        }
    };

    const move = e => {
        if (!pointers.has(e.pointerId)) return;
        pointers.set(e.pointerId, { x: e.clientX, y: e.clientY });
        if (pinch && pointers.size === 2) {
            const m = middle();
            zoomAt(pinch.scale * distance() / pinch.distance, m.x, m.y);
        } else if (pan && pointers.size === 1 && scale > 1) {
            x = e.clientX - pan.x;
            y = e.clientY - pan.y;
            clamp();
            apply();
        }
    };

    const up = e => {
        pointers.delete(e.pointerId);
        if (pointers.size < 2) pinch = null;
        if (pointers.size === 1) {
            const [p] = [...pointers.values()];
            pan = { x: p.x - x, y: p.y - y };
        } else if (pointers.size === 0) {
            pan = null;
        }
    };

    container.addEventListener('pointerdown', down);
    container.addEventListener('pointermove', move);
    container.addEventListener('pointerup', up);
    container.addEventListener('pointercancel', up);
    apply();

    return {
        detach() {
            container.removeEventListener('pointerdown', down);
            container.removeEventListener('pointermove', move);
            container.removeEventListener('pointerup', up);
            container.removeEventListener('pointercancel', up);
        },
        // Para pruebas: escala actual.
        scale: () => scale,
        zoomTo: s => { const r = container.getBoundingClientRect(); zoomAt(s, r.left + r.width / 2, r.top + r.height / 2); },
    };
}
