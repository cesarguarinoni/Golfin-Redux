import type { Metadata } from "next";
import { TelemetryPanel } from "./telemetry-panel";

export const metadata: Metadata = { title: "Telemetry — GOLFIN Admin" };
export const dynamic = "force-dynamic";

export default function TelemetryPage() {
  return <TelemetryPanel />;
}
