import { ExportOrchestrator } from '../services/exportOrchestrator.js';
const orchestrator = new ExportOrchestrator();
export const exportRoutes = async (app) => {
    // POST /api/export/start
    app.post('/start', async (request, reply) => {
        const config = request.body;
        try {
            const job = orchestrator.start(config);
            return { jobId: job.id, totalModels: job.total };
        }
        catch (err) {
            const message = err instanceof Error ? err.message : 'Export failed to start';
            return reply.status(500).send({ error: message });
        }
    });
    // DELETE /api/export/cancel
    app.delete('/cancel', async () => {
        const cancelled = orchestrator.cancel();
        return { cancelled };
    });
    // GET /api/export/status
    app.get('/status', async () => {
        return orchestrator.getStatus();
    });
    // GET /api/export/progress — Server-Sent Events
    app.get('/progress', async (request, reply) => {
        reply.raw.writeHead(200, {
            'Content-Type': 'text/event-stream',
            'Cache-Control': 'no-cache',
            'Connection': 'keep-alive',
        });
        const unsubscribe = orchestrator.onProgress((event) => {
            reply.raw.write(`data: ${JSON.stringify(event)}\n\n`);
        });
        request.raw.on('close', () => {
            unsubscribe();
        });
    });
};
//# sourceMappingURL=export.js.map