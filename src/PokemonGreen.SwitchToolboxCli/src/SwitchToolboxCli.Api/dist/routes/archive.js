import { scanArchive } from '../services/archiveScanner.js';
export const archiveRoutes = async (app) => {
    // GET /api/archive/scan?path=<arcDir>
    app.get('/scan', async (request, reply) => {
        const { path: arcPath } = request.query;
        if (!arcPath) {
            return reply.status(400).send({ error: 'Missing "path" query parameter' });
        }
        try {
            const result = await scanArchive(arcPath);
            return result;
        }
        catch (err) {
            const message = err instanceof Error ? err.message : 'Scan failed';
            return reply.status(500).send({ error: message });
        }
    });
};
//# sourceMappingURL=archive.js.map