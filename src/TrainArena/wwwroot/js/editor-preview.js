(() => {
  const fmt = window.TrainArenaQuestionFormat;
  if (!fmt?.renderQuestionText) {
    return;
  }

  function bindTextarea(textarea) {
    const compose = textarea.closest(".question-compose");
    const preview = compose?.querySelector(".js-question-preview");
    if (!preview) {
      return;
    }

    const update = () => fmt.renderQuestionText(preview, textarea.value);
    textarea.addEventListener("input", update);
    update();
  }

  function renderStaticPreviews() {
    document.querySelectorAll("[data-q-preview-for]").forEach((target) => {
      const id = target.getAttribute("data-q-preview-for");
      const src = id ? document.getElementById(id) : null;
      if (!src) {
        return;
      }
      const text = src instanceof HTMLTextAreaElement ? src.value : src.textContent;
      fmt.renderQuestionText(target, text ?? "");
    });
  }

  document.querySelectorAll(".js-question-text-input").forEach(bindTextarea);
  renderStaticPreviews();
})();
