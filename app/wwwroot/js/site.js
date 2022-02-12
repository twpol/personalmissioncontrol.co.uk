function seamlessResize() {
  const frames = document.querySelectorAll("iframe.seamless");
  for (const frame of frames) {
    frame.height = frame.contentDocument.documentElement.offsetHeight;
    for (const link of frame.contentDocument.querySelectorAll("a[href]")) {
      link.target = "_blank";
    }
    for (const image of frame.contentDocument.querySelectorAll("img[src]")) {
      image.addEventListener("load", seamlessLoaded);
      image.referrerPolicy = "no-referrer";
    }
  }
}

function seamlessLoaded() {
  seamlessResize();
  if (document.querySelector("ul.messages")) {
    messageAutoScroll();
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
const messagesSummary = document.createElement("div");
let messageIsReading = true;

function messageAutoScroll() {
  const offset = document.documentElement.scrollTop;
  let top = 0;
  let found = false;
  for (const message of document.querySelectorAll("ul.messages > li")) {
    top = message.getBoundingClientRect().top + offset;
    if (message.dataset.unread === "True") {
      found = true;
      break;
    }
  }
  if (found) {
    document.documentElement.scrollTo(0, top - MESSAGE_TOP_OFFSET);
  } else {
    document.documentElement.scrollTo(0, document.documentElement.scrollHeight);
  }

  messageHighlightScroll();
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
    messageUpdateUnreadScrollDB();
  }
}

const apiStatus = new URL(
  `/api/${location.pathname.split("/")[1]}/email/status`,
  location
);

async function setMessageStatus(message, field, value) {
  if (value === true) value = "True";
  if (value === false) value = "False";
  apiStatus.search = new URLSearchParams([
    ["id", message.dataset.id],
    [field, value],
  ]).toString();
  await fetch(apiStatus, { method: "POST" });
  message.dataset[field] = value;
  if (field === "flagged" && value) message.dataset.completed = "False";
  if (field === "completed" && value) message.dataset.flagged = "False";
  messagesUpdateSummary(message);
}

async function messageUpdateUnreadScroll() {
  if (!messageIsReading) return;
  const currentMessage = document.querySelector("ul.messages > li.current");
  let unread = false;
  for (const message of document.querySelectorAll("ul.messages > li")) {
    if (message === currentMessage) unread = true;
    const messageUnread = message.dataset.unread === "True";
    if (messageUnread !== unread) {
      await setMessageStatus(message, "unread", unread);
    } else if (unread) {
      break;
    }
  }
}

const messageUpdateUnreadScrollDB = debounce(5000, messageUpdateUnreadScroll);

function messageKeyDown(event) {
  const currentMessage = document.querySelector("ul.messages > li.current");
  const flagged = currentMessage?.dataset.flagged === "True";
  const completed = currentMessage?.dataset.completed === "True";
  const key = [
    event.altKey ? "Alt+" : "",
    event.ctrlKey ? "Control+" : "",
    event.shiftKey ? "Shift+" : "",
    event.metaKey ? "Meta+" : "",
    event.code,
  ].join("");
  switch (key) {
    case "Insert":
      if (!currentMessage) return;
      if (!completed) {
        setMessageStatus(currentMessage, "flagged", !flagged);
      }
      break;
    case "Control+Insert":
      if (!currentMessage) return;
      if (flagged) {
        setMessageStatus(currentMessage, "completed", true);
      } else if (completed) {
        setMessageStatus(currentMessage, "flagged", true);
      }
      break;
    case "Pause":
      messageIsReading = !messageIsReading;
      if (messageIsReading) {
        messageAutoScroll();
      }
      showAlert("info", messageIsReading ? "Reading mode" : "Review mode");
      break;
  }
}

function messagesCreateSummary() {
  for (const message of document.querySelectorAll("ul.messages > li")) {
    const messageSummary = document.createElement("div");
    messageSummary.innerHTML =
      '<i class="bi bi-envelope-fill text-blue"></i>' +
      '<i class="bi bi-envelope-open-fill text-grey"></i>' +
      '<i class="bi bi-flag-fill text-red"></i>' +
      '<i class="bi bi-flag-fill text-grey"></i>' +
      '<i class="bi bi-check-circle-fill text-green"></i>' +
      '<i class="bi bi-check-circle-fill text-grey"></i>';
    message.dataset.index = messagesSummary.childElementCount;
    messagesSummary.append(messageSummary);
    messagesUpdateSummary(message);
  }
  messagesSummary.classList.add("messages-summary");
  document
    .querySelector("ul.messages")
    .insertAdjacentElement("afterend", messagesSummary);
}

function messagesUpdateSummary(message) {
  const summary = messagesSummary.children[message.dataset.index];
  for (const name of Object.keys(message.dataset)) {
    summary.dataset[name] = message.dataset[name];
  }
}

if (document.querySelector("ul.messages")) {
  window.addEventListener("load", function () {
    // Delay scroll by a tiny fraction so we override the browser's build in scroll position restoration
    setTimeout(messageAutoScroll, 100);
    setTimeout(messagesCreateSummary, 100);
    document.addEventListener("scroll", debounce(100, messageHighlightScroll));
  });

  window.addEventListener("keydown", messageKeyDown);
}

function showAlert(type, message) {
  const alert = document.createElement("div");
  alert.classList.add("alert", "alert--transient", `alert-${type}`);
  alert.innerText = message;
  alert.setAttribute("role", "alert");
  document.body.append(alert);
  setTimeout(() => alert.remove(), 5000);
}

function debounce(delay, callback) {
  let timeout;
  return function () {
    const args = arguments;
    if (timeout) {
      window.clearTimeout(timeout);
    }
    timeout = window.setTimeout(() => callback.apply(this, args), delay);
  };
}
