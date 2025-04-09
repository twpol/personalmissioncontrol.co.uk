export function debounce(delay, callback) {
    let timeout;
    return function () {
      const args = arguments;
      if (timeout) {
        window.clearTimeout(timeout);
      }
      timeout = window.setTimeout(() => callback.apply(this, args), delay);
    };
  }
  