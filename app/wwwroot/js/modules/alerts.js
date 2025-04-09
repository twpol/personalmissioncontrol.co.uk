export function showAlert(type, message) {
  const alert = document.createElement("div");
  alert.classList.add("alert", "alert--transient", `alert-${type}`);
  alert.innerText = message;
  alert.setAttribute("role", "alert");
  document.body.append(alert);
  setTimeout(() => alert.remove(), 5000);
}
