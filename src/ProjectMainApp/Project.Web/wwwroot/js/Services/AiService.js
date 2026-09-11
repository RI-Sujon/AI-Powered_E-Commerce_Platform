const AiService = {
    baseUrl: `${window.API_BASE_URL}/api/ai`,

    // Phase 4 — admin product-description draft. Never saves anything; caller decides.
    async generateDescription(productId, options) {
        const response = await $.ajax({
            url: `${this.baseUrl}/products/${productId}/generate-description`,
            method: 'POST',
            contentType: 'application/json',
            headers: ServiceUtils.getHeaders(),
            data: JSON.stringify(options || {})
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Failed to generate a description');
    },

    // Phase 5 — semantic product search. Anonymous-friendly (getHeaders() works with no token).
    async searchProducts(query, take) {
        const response = await $.ajax({
            url: `${this.baseUrl}/products/search`,
            method: 'GET',
            headers: ServiceUtils.getHeaders(),
            data: { q: query, take: take || 12 }
        });
        if (response.isSuccess) return response.data;
        throw new Error(response.errorMessage || 'Search failed');
    },

    /**
     * Phase 6 — shopping-assistant chat. Uses `fetch` rather than jQuery's `$.ajax`: the
     * response is Server-Sent Events and needs an incremental body reader, which $.ajax
     * doesn't expose. Every other AiService method stays on $.ajax to match the rest of
     * the codebase's convention.
     *
     * messages: [{ role: 'user'|'assistant', content: string }, ...]
     * callbacks: { onDelta(text), onDone(), onError(err) }, plus an optional AbortSignal.
     */
    async streamChat(messages, { onDelta, onDone, onError, signal } = {}) {
        try {
            const headers = Object.assign({ 'Content-Type': 'application/json' }, ServiceUtils.getHeaders());
            const res = await fetch(`${this.baseUrl}/assistant/chat`, {
                method: 'POST',
                headers,
                body: JSON.stringify({ messages }),
                signal
            });
            if (!res.ok || !res.body) {
                throw new Error(`The assistant is unavailable right now (HTTP ${res.status}).`);
            }

            const reader = res.body.getReader();
            const decoder = new TextDecoder();
            let buffer = '';
            let sawError = null;

            while (true) {
                const { value, done } = await reader.read();
                if (done) break;

                buffer += decoder.decode(value, { stream: true });
                const frames = buffer.split('\n\n');
                buffer = frames.pop(); // keep the last (possibly incomplete) frame for next read

                for (const frame of frames) {
                    const lines = frame.split('\n');
                    const isErrorFrame = lines.some(l => l.startsWith('event: error'));
                    const dataLine = lines.find(l => l.startsWith('data: '));
                    if (!dataLine) continue;

                    const payload = dataLine.slice(6);
                    if (payload === '[DONE]') {
                        if (sawError) onError && onError(new Error(sawError));
                        else onDone && onDone();
                        return;
                    }

                    let text;
                    try { text = JSON.parse(payload); } catch { continue; }

                    if (isErrorFrame) sawError = text;
                    else onDelta && onDelta(text);
                }
            }

            if (sawError) onError && onError(new Error(sawError));
            else onDone && onDone();
        } catch (e) {
            if (e.name === 'AbortError') return;
            onError && onError(e);
        }
    }
};

window.AiService = AiService;
