/**
 * AssistantWidget — Phase 6 shopping-assistant chat bubble.
 * Requires: jQuery, AiService, and the #assistant-widget markup (_AssistantWidget.cshtml).
 * Conversation lives in memory + sessionStorage only (no server persistence — matches the
 * stateless /api/ai/assistant/chat endpoint).
 */
const AssistantWidget = {
    _messages: [],       // [{ role: 'user'|'assistant', content }]
    _busy: false,
    _controller: null,

    init() {
        if (!$('#assistant-widget').length) return; // partial not on this page
        this._restore();
        $('#assistant-fab').on('click', () => this.open());
        $('#assistant-close').on('click', () => this.close());
        $('#assistant-form').on('submit', (e) => { e.preventDefault(); this._send(); });
    },

    open() {
        $('#assistant-panel').removeClass('d-none');
        $('#assistant-fab').addClass('d-none');
        if (!this._messages.length) this._appendBubble('assistant',
            'Hi! I can help you find products in our store — try “a gift for a 10 year old ' +
            'who likes science, under $40”.');
        $('#assistant-input').trigger('focus');
    },

    close() {
        $('#assistant-panel').addClass('d-none');
        $('#assistant-fab').removeClass('d-none');
        if (this._controller) this._controller.abort();
    },

    _restore() {
        try {
            const saved = sessionStorage.getItem('assistant_messages');
            if (!saved) return;
            this._messages = JSON.parse(saved) || [];
            this._messages.forEach(m => this._appendBubble(m.role, m.content));
        } catch {
            this._messages = []; // corrupt/unavailable storage - start fresh, non-fatal
        }
    },

    _persist() {
        try {
            // Keep only what the server actually uses (last 12) plus a little slack for display.
            sessionStorage.setItem('assistant_messages', JSON.stringify(this._messages.slice(-20)));
        } catch { /* storage full or blocked - conversation still works this session */ }
    },

    _appendBubble(role, text) {
        const cls = role === 'user' ? 'assistant-msg-user'
                  : role === 'error' ? 'assistant-msg-error'
                  : 'assistant-msg-assistant';
        const $bubble = $('<div>').addClass('assistant-msg').addClass(cls).text(text);
        $('#assistant-messages').append($bubble);
        this._scrollToBottom();
        return $bubble;
    },

    _scrollToBottom() {
        const el = document.getElementById('assistant-messages');
        if (el) el.scrollTop = el.scrollHeight;
    },

    async _send() {
        if (this._busy) return;

        const $input = $('#assistant-input');
        const text = $input.val().trim();
        if (!text) return;

        $input.val('');
        this._appendBubble('user', text);
        this._messages.push({ role: 'user', content: text });
        this._persist();

        this._busy = true;
        $('#assistant-send').prop('disabled', true);

        const $typing = $('<div class="assistant-msg assistant-msg-assistant assistant-typing">')
            .html('<span></span><span></span><span></span>');
        $('#assistant-messages').append($typing);
        this._scrollToBottom();

        let $reply = null;
        let replyText = '';
        this._controller = new AbortController();

        await AiService.streamChat(this._messages, {
            signal: this._controller.signal,
            onDelta: (delta) => {
                if (!$reply) {
                    $typing.remove();
                    $reply = this._appendBubble('assistant', '');
                }
                replyText += delta;
                $reply.text(replyText);
                this._scrollToBottom();
            },
            onDone: () => {
                $typing.remove();
                if (replyText) {
                    this._messages.push({ role: 'assistant', content: replyText });
                    this._persist();
                }
                this._finishSending();
            },
            onError: (err) => {
                $typing.remove();
                if (!$reply) this._appendBubble('error', err.message || 'The assistant is unavailable right now.');
                this._finishSending();
            }
        });
    },

    _finishSending() {
        this._busy = false;
        $('#assistant-send').prop('disabled', false);
    }
};

window.AssistantWidget = AssistantWidget;
$(document).ready(() => AssistantWidget.init());
