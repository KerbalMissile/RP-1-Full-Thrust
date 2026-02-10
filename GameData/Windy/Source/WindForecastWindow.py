import os
import time
import tkinter as tk
from tkinter import ttk, filedialog

# Simple external window to display Windy data file
# Beginner-friendly Python script using tkinter.

# CHANGE THIS TO YOUR KSP FOLDER IF YOU WANT A DEFAULT
DEFAULT_KSP_ROOT = r"D:\SteamLibrary\steamapps\common\ModTestingKSP"
DEFAULT_DATA_FILE = os.path.join(DEFAULT_KSP_ROOT, "WindyWindData.txt")

POLL_MS = 250  # ms between refreshes


def parse_wind_file(path):
    """Read key=value lines into a dict of floats where possible."""
    if not os.path.exists(path):
        return None

    data = {}
    try:
        with open(path, "r", encoding="utf-8", errors="replace") as f:
            for line in f:
                line = line.strip()
                if not line or "=" not in line:
                    continue
                key, val = line.split("=", 1)
                key = key.strip().lower()
                val = val.strip()
                try:
                    data[key] = float(val)
                except ValueError:
                    data[key] = val
    except OSError:
        return None

    # Require at least speed and direction to consider this valid
    if "speed" not in data or "direction_deg" not in data:
        return None

    return data


class WindMonitorApp(tk.Tk):
    def __init__(self):
        super().__init__()

        self.title("Windy Monitor + Forecasts")
        self.geometry("460x380")
        self.resizable(False, False)

        self.data_file_var = tk.StringVar(value=DEFAULT_DATA_FILE)

        # Current wind labels
        self.speed_var = tk.StringVar(value="—")
        self.dir_var = tk.StringVar(value="—")
        self.alt_var = tk.StringVar(value="—")

        # Forecast labels
        self.f5_speed_var = tk.StringVar(value="—")
        self.f5_dir_var = tk.StringVar(value="—")

        self.f10_speed_var = tk.StringVar(value="—")
        self.f10_dir_var = tk.StringVar(value="—")

        self.f15_speed_var = tk.StringVar(value="—")
        self.f15_dir_var = tk.StringVar(value="—")

        # Status
        self.status_var = tk.StringVar(value="Waiting for WindyWindData.txt...")

        self._last_mtime = None

        self._build_ui()
        self.after(POLL_MS, self._poll)

    def _build_ui(self):
        pad = {"padx": 10, "pady": 5}

        frame = ttk.Frame(self)
        frame.pack(fill="both", expand=True)

        # File selection row
        ttk.Label(frame, text="Data file:").grid(row=0, column=0, sticky="w", **pad)
        entry = ttk.Entry(frame, textvariable=self.data_file_var, width=40)
        entry.grid(row=1, column=0, columnspan=3, sticky="we", padx=10, pady=2)

        browse_btn = ttk.Button(frame, text="Browse...", command=self._browse)
        browse_btn.grid(row=1, column=3, sticky="e", padx=10, pady=2)

        # Current wind section
        ttk.Label(
            frame,
            text="CURRENT WIND",
            font=("Segoe UI", 10, "bold")
        ).grid(row=2, column=0, columnspan=4, sticky="w", **pad)

        ttk.Label(frame, text="Speed (m/s):").grid(row=3, column=0, sticky="w", **pad)
        ttk.Label(
            frame,
            textvariable=self.speed_var,
            font=("Segoe UI", 14, "bold")
        ).grid(row=3, column=1, columnspan=3, sticky="w", **pad)

        ttk.Label(frame, text="Direction (deg):").grid(row=4, column=0, sticky="w", **pad)
        ttk.Label(
            frame,
            textvariable=self.dir_var,
            font=("Segoe UI", 14, "bold")
        ).grid(row=4, column=1, columnspan=3, sticky="w", **pad)

        ttk.Label(frame, text="Altitude (m):").grid(row=5, column=0, sticky="w", **pad)
        ttk.Label(
            frame,
            textvariable=self.alt_var,
            font=("Segoe UI", 12)
        ).grid(row=5, column=1, columnspan=3, sticky="w", **pad)

        # Forecast section
        ttk.Label(
            frame,
            text="FORECASTS",
            font=("Segoe UI", 10, "bold")
        ).grid(row=6, column=0, columnspan=4, sticky="w", **pad)

        # +5 min
        ttk.Label(frame, text="+5 min:").grid(row=7, column=0, sticky="w", **pad)
        ttk.Label(frame, textvariable=self.f5_speed_var).grid(row=7, column=1, sticky="w", **pad)
        ttk.Label(frame, text="m/s @").grid(row=7, column=2, sticky="e", **pad)
        ttk.Label(frame, textvariable=self.f5_dir_var).grid(row=7, column=3, sticky="w", **pad)

        # +10 min
        ttk.Label(frame, text="+10 min:").grid(row=8, column=0, sticky="w", **pad)
        ttk.Label(frame, textvariable=self.f10_speed_var).grid(row=8, column=1, sticky="w", **pad)
        ttk.Label(frame, text="m/s @").grid(row=8, column=2, sticky="e", **pad)
        ttk.Label(frame, textvariable=self.f10_dir_var).grid(row=8, column=3, sticky="w", **pad)

        # +15 min
        ttk.Label(frame, text="+15 min:").grid(row=9, column=0, sticky="w", **pad)
        ttk.Label(frame, textvariable=self.f15_speed_var).grid(row=9, column=1, sticky="w", **pad)
        ttk.Label(frame, text="m/s @").grid(row=9, column=2, sticky="e", **pad)
        ttk.Label(frame, textvariable=self.f15_dir_var).grid(row=9, column=3, sticky="w", **pad)

        # Status line
        ttk.Label(
            frame,
            textvariable=self.status_var,
            foreground="#666666"
        ).grid(row=10, column=0, columnspan=4, sticky="w", padx=10, pady=12)

        frame.columnconfigure(0, weight=1)
        frame.columnconfigure(1, weight=0)
        frame.columnconfigure(2, weight=0)
        frame.columnconfigure(3, weight=0)

    def _browse(self):
        path = filedialog.askopenfilename(
            title="Select WindyWindData.txt",
            filetypes=[("Text files", "*.txt"), ("All files", "*.*")]
        )
        if path:
            self.data_file_var.set(path)
            self._last_mtime = None

    def _poll(self):
        path = self.data_file_var.get().strip()

        if not path:
            self.status_var.set("No file selected.")
            self.after(POLL_MS, self._poll)
            return

        if not os.path.exists(path):
            # reset display
            self.speed_var.set("—")
            self.dir_var.set("—")
            self.alt_var.set("—")
            self.f5_speed_var.set("—")
            self.f5_dir_var.set("—")
            self.f10_speed_var.set("—")
            self.f10_dir_var.set("—")
            self.f15_speed_var.set("—")
            self.f15_dir_var.set("—")
            self.status_var.set(f"File not found: {path}")
            self.after(POLL_MS, self._poll)
            return

        try:
            mtime = os.path.getmtime(path)
        except OSError:
            mtime = None

        if mtime != self._last_mtime:
            self._last_mtime = mtime
            data = parse_wind_file(path)

            if data is None:
                self.speed_var.set("—")
                self.dir_var.set("—")
                self.alt_var.set("—")
                self.status_var.set("Invalid or incomplete data.")
            else:
                # Current wind
                self.speed_var.set(f"{data.get('speed', 0.0):.1f}")
                self.dir_var.set(f"{data.get('direction_deg', 0.0):.0f}°")
                if "altitude" in data:
                    self.alt_var.set(f"{data['altitude']:.0f}")
                else:
                    self.alt_var.set("—")

                # Forecasts (if keys are present)
                self.f5_speed_var.set(
                    f"{data.get('forecast_5min_speed', 0.0):.1f}"
                    if "forecast_5min_speed" in data else "—"
                )
                self.f5_dir_var.set(
                    f"{data.get('forecast_5min_dir', 0.0):.0f}°"
                    if "forecast_5min_dir" in data else "—"
                )

                self.f10_speed_var.set(
                    f"{data.get('forecast_10min_speed', 0.0):.1f}"
                    if "forecast_10min_speed" in data else "—"
                )
                self.f10_dir_var.set(
                    f"{data.get('forecast_10min_dir', 0.0):.0f}°"
                    if "forecast_10min_dir" in data else "—"
                )

                self.f15_speed_var.set(
                    f"{data.get('forecast_15min_speed', 0.0):.1f}"
                    if "forecast_15min_speed" in data else "—"
                )
                self.f15_dir_var.set(
                    f"{data.get('forecast_15min_dir', 0.0):.0f}°"
                    if "forecast_15min_dir" in data else "—"
                )

                # Age info
                ts = data.get("timestamp_unix")
                if isinstance(ts, (int, float)):
                    age = time.time() - ts
                    if age < 0:
                        age = 0
                    self.status_var.set(f"Updated {age:.1f}s ago")
                else:
                    self.status_var.set("Updated")

        self.after(POLL_MS, self._poll)


if __name__ == "__main__":
    WindMonitorApp().mainloop()