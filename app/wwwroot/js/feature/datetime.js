function dateTimeLocal() {
  /** @type {NodeListOf<HTMLElement>} */
  const elements = document.querySelectorAll(".datetime-local[data-ts]");
  for (const element of elements) {
    element.textContent = new Date(element.dataset.ts || "").toLocaleString();
  }
}

window.addEventListener("load", dateTimeLocal);
