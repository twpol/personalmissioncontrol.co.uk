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

const MESSAGE_TOP_OFFSET = 200;

function messageAutoScroll() {
  const offset = document.documentElement.scrollTop;
  let top = 0;
  for (const message of document.querySelectorAll("ul.messages > li")) {
    top = message.getBoundingClientRect().top + offset;
    if (message.dataset.unread === "True") {
      break;
    }
  }
  document.documentElement.scrollTo(0, top - MESSAGE_TOP_OFFSET);

  messageHighlightScroll();
  document.addEventListener("scroll", debounce(messageHighlightScroll));

  // TODO: Enable scrolling-based email unread status here
}

function messageHighlightScroll() {
  const oldCurrentMessage = document.querySelector("ul.messages > li.current");
  const currentMessage = Array.of(
    ...document.querySelectorAll("ul.messages > li")
  ).find(
    (message) => message.getBoundingClientRect().bottom > MESSAGE_TOP_OFFSET
  );
  if (oldCurrentMessage !== currentMessage) {
    if (oldCurrentMessage) {
      oldCurrentMessage.classList.remove("current", "border-primary");
    }
    if (currentMessage) {
      currentMessage.classList.add("current", "border-primary");
    }
  }
}

if (document.querySelector("ul.messages")) {
  window.addEventListener("load", function () {
    // Delay scroll by a tiny fraction so we override the browser's build in scroll position restoration
    setTimeout(messageAutoScroll, 100);
  });
}

function debounce(callback) {
  let timeout;
  return function () {
    const args = arguments;
    if (timeout) {
      window.cancelAnimationFrame(timeout);
    }
    timeout = window.requestAnimationFrame(() => callback.apply(this, args));
  };
}
