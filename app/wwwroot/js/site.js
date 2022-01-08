function seamlessResize() {
  const frames = document.querySelectorAll("iframe.seamless");
  for (const frame of frames) {
    frame.height = frame.contentDocument.documentElement.offsetHeight;
  }
}

window.addEventListener("resize", seamlessResize);
window.addEventListener("load", seamlessResize);

function dateTimeLocal() {
  const elements = document.querySelectorAll(".datetime-local[data-ts]");
  for (const element of elements) {
    element.textContent = new Date(element.dataset.ts).toLocaleString();
  }
}

window.addEventListener("load", dateTimeLocal);
