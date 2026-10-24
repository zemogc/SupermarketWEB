// Comportamientos declarativos reutilizables. Se usan atributos data-* en lugar de
// manejadores inline porque la Content-Security-Policy no permite scripts en línea.

// <form data-confirm="¿Seguro?">: pide confirmación antes de enviar.
document.addEventListener('submit', (event) => {
    const message = event.target.dataset.confirm;
    if (message && !window.confirm(message)) {
        event.preventDefault();
    }
});

// <button data-print>: abre el diálogo de impresión.
document.addEventListener('click', (event) => {
    if (event.target.closest('[data-print]')) {
        window.print();
    }
});
