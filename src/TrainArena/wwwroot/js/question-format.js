(() => {
  const FENCE_RE = /```([a-zA-Z0-9_+-]*)\r?\n([\s\S]*?)```/g;

  function escapeHtml(text) {
    return String(text)
      .replace(/&/g, "&amp;")
      .replace(/</g, "&lt;")
      .replace(/>/g, "&gt;")
      .replace(/"/g, "&quot;");
  }

  function formatPlain(text) {
    return escapeHtml(text).replace(/\r?\n/g, "<br>");
  }

  function highlight(root) {
    if (!root || typeof Prism === "undefined" || !Prism.highlightAllUnder) return;
    try {
      Prism.highlightAllUnder(root);
    } catch {
      // ignore highlight failures
    }
  }

  /**
   * Renders quiz text with optional fenced code blocks into el (safe HTML).
   * Fences: ```csharp ... ``` (language optional, default csharp).
   */
  function renderQuestionText(el, text) {
    if (!el) return;
    const raw = text == null ? "" : String(text);
    if (!raw.includes("```")) {
      el.innerHTML = formatPlain(raw);
      return;
    }

    let html = "";
    let last = 0;
    FENCE_RE.lastIndex = 0;
    let match;
    while ((match = FENCE_RE.exec(raw)) !== null) {
      html += formatPlain(raw.slice(last, match.index));
      const lang = (match[1] || "csharp").toLowerCase();
      const code = escapeHtml(match[2].replace(/\n$/, ""));
      html += `<pre class="q-code"><code class="language-${lang}">${code}</code></pre>`;
      last = match.index + match[0].length;
    }
    html += formatPlain(raw.slice(last));
    el.innerHTML = html;
    highlight(el);
  }

  window.TrainArenaQuestionFormat = { renderQuestionText, escapeHtml };
})();
