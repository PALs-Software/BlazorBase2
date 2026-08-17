const charts = {};

export function createChart(elementId, config) {
    destroyChart(elementId);
    const ctx = document.getElementById(elementId);
    if (!ctx) return;
    charts[elementId] = new Chart(ctx, config);
}

export function updateChart(elementId, config) {
    const chart = charts[elementId];
    if (!chart) {
        createChart(elementId, config);
        return;
    }
    chart.data = config.data;
    if (config.options) chart.options = config.options;
    chart.update();
}

export function destroyChart(elementId) {
    const chart = charts[elementId];
    if (chart) {
        chart.destroy();
        delete charts[elementId];
    }
}
