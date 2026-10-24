// Formulario de nueva venta: agrega/quita filas de productos y muestra subtotales estimados.
// El servidor recalcula el total con los precios vigentes, así que esto es solo informativo.
(() => {
    const form = document.querySelector('[data-sale-form]');
    if (!form) {
        return;
    }

    const body = form.querySelector('[data-lines]');
    const template = document.getElementById('sale-line-template');
    const totalLabel = form.querySelector('[data-total]');
    const currency = new Intl.NumberFormat('es-CO', {
        style: 'currency',
        currency: 'COP',
        minimumFractionDigits: 0,
        maximumFractionDigits: 2,
    });

    const rows = () => Array.from(body.querySelectorAll('[data-line]'));

    // El model binding necesita índices consecutivos desde 0.
    function reindex() {
        rows().forEach((row, index) => {
            row.querySelectorAll('[name]').forEach((field) => {
                field.name = field.name.replace(/Lines\[[^\]]*\]/, `Lines[${index}]`);
            });
        });
    }

    function recalculate() {
        let total = 0;

        rows().forEach((row) => {
            const option = row.querySelector('[data-product]').selectedOptions[0];
            const price = Number.parseFloat(option?.dataset.price ?? '');
            const quantity = Number.parseInt(row.querySelector('[data-quantity]').value, 10);
            const subtotal = price > 0 && quantity > 0 ? price * quantity : 0;

            row.querySelector('[data-subtotal]').textContent = subtotal > 0 ? currency.format(subtotal) : '—';
            total += subtotal;
        });

        totalLabel.textContent = currency.format(total);
    }

    form.querySelector('[data-add-line]').addEventListener('click', () => {
        body.appendChild(template.content.cloneNode(true));
        reindex();
        recalculate();
        body.lastElementChild.querySelector('[data-product]').focus();
    });

    body.addEventListener('click', (event) => {
        const button = event.target.closest('[data-remove-line]');
        if (!button) {
            return;
        }

        const row = button.closest('[data-line]');
        if (rows().length > 1) {
            row.remove();
        } else {
            row.querySelector('[data-product]').value = '';
            row.querySelector('[data-quantity]').value = '1';
        }

        reindex();
        recalculate();
    });

    body.addEventListener('input', recalculate);
    body.addEventListener('change', recalculate);
    recalculate();
})();
