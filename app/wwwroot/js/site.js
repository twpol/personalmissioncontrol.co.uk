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
    messageUpdateUnreadScrollDBL();
  }
}

const apiStatus = new URL(
  `/api/${location.pathname.split("/")[1]}/email/status`,
  location
);

function setMessageStatus(message, field, value) {
  if (value === true) value = "True";
  if (value === false) value = "False";
  apiStatus.search = new URLSearchParams([
    ["id", message.dataset.id],
    [field, value],
  ]).toString();
  fetch(apiStatus, { method: "POST" });
  message.dataset[field] = value;
  if (field === "flagged" && value) message.dataset.completed = "False";
  if (field === "completed" && value) message.dataset.flagged = "False";
}

function messageUpdateUnreadScroll() {
  const currentMessage = document.querySelector("ul.messages > li.current");
  let unread = false;
  for (const message of document.querySelectorAll("ul.messages > li")) {
    if (message === currentMessage) unread = true;
    const messageUnread = message.dataset.unread === "True";
    if (messageUnread !== unread) {
      setMessageStatus(message, "unread", unread);
    } else if (unread) {
      break;
    }
  }
}

const messageUpdateUnreadScrollDBL = debounceLazy(
  5000,
  messageUpdateUnreadScroll
);

function messageKeyDown(event) {
  const currentMessage = document.querySelector("ul.messages > li.current");
  if (!currentMessage) return;
  const key = [
    event.altKey ? "Alt+" : "",
    event.ctrlKey ? "Control+" : "",
    event.shiftKey ? "Shift+" : "",
    event.metaKey ? "Meta+" : "",
    event.code,
  ].join("");
  const flagged = currentMessage.dataset.flagged === "True";
  const completed = currentMessage.dataset.completed === "True";
  switch (key) {
    case "Insert":
      if (!completed) {
        setMessageStatus(currentMessage, "flagged", !flagged);
      }
      break;
    case "Control+Insert":
      if (flagged) {
        setMessageStatus(currentMessage, "completed", true);
      } else if (completed) {
        setMessageStatus(currentMessage, "flagged", true);
      }
      break;
  }
}

if (document.querySelector("ul.messages")) {
  window.addEventListener("load", function () {
    // Delay scroll by a tiny fraction so we override the browser's build in scroll position restoration
    setTimeout(messageAutoScroll, 100);
  });

  window.addEventListener("keydown", messageKeyDown);
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

function debounceLazy(delay, callback) {
  let timeout;
  return function () {
    const args = arguments;
    if (timeout) {
      window.clearTimeout(timeout);
    }
    timeout = window.setTimeout(() => callback.apply(this, args), delay);
  };
}
