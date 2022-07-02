import { showAlert } from "../modules/alerts.js";

if (document.querySelector(".navigation-series")) {
  const series = Array.from(
    /** @type {NodeListOf<HTMLAnchorElement>} */ (
      document.querySelectorAll(".navigation-series a[href]")
    )
  ).map((element) => element.href);
  sessionStorage["pmc-navigation-up"] = location.href;
  sessionStorage["pmc-navigation-series"] = JSON.stringify(series);
}

const navigationSeries = {
  up: "",
  next: "",
  prev: "",
};

function navigationSeriesKeyDown(event) {
  const key = [
    event.altKey ? "Alt+" : "",
    event.ctrlKey ? "Control+" : "",
    event.shiftKey ? "Shift+" : "",
    event.metaKey ? "Meta+" : "",
    event.code,
  ].join("");
  switch (key) {
    case "Control+ArrowUp":
      if (navigationSeries.up) {
        showAlert("info", "Navigating up...");
        location.href = navigationSeries.up;
      }
      event.preventDefault();
      break;
    case "ArrowLeft":
      if (navigationSeries.prev) {
        showAlert("info", "Navigating prev...");
        location.href = navigationSeries.prev;
      }
      event.preventDefault();
      break;
    case "ArrowRight":
      if (navigationSeries.next) {
        showAlert("info", "Navigating next...");
        location.href = navigationSeries.next;
      }
      event.preventDefault();
      break;
  }
}

if (sessionStorage["pmc-navigation-series"]) {
  const series = JSON.parse(sessionStorage["pmc-navigation-series"]);
  const index = series.indexOf(location.href);
  if (index >= 0) {
    navigationSeries.up = sessionStorage["pmc-navigation-up"];
    navigationSeries.prev = series[index - 1];
    navigationSeries.next = series[index + 1];
    window.addEventListener("keydown", navigationSeriesKeyDown);
  }
}
