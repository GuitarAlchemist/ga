import { promises as fs } from "node:fs";
import { spawn } from "node:child_process";
import { Server } from "@modelcontextprotocol/sdk/server";
import {
  CallToolRequest,
  CallToolResult,
  Tool
} from "@modelcontextprotocol/sdk/types";

const PYTHON_CMD = process.env.PYTHON || "python";
const ANALYZER_MODULE = "audio_analyzer.server";

const analyzeTool: Tool = {
  name: "analyze_audio_file",
  description:
    "Analyse un fichier audio (spectre, transitoires, loudness, embedding CLAP).",
  inputSchema: {
    type: "object",
    properties: {
      path: {
        type: "string",
        description:
          "Chemin absolu vers le fichier audio sur la machine du MCP."
      }
    },
    required: ["path"]
  }
};

async function runPythonAnalyze(path: string): Promise<any> {
  return new Promise((resolve, reject) => {
    const proc = spawn(
      PYTHON_CMD,
      ["-m", ANALYZER_MODULE, "analyze-file", path],
      { stdio: ["ignore", "pipe", "pipe"] }
    );

    let out = "";
    let err = "";

    proc.stdout.on("data", chunk => {
      out += chunk.toString("utf8");
    });

    proc.stderr.on("data", chunk => {
      err += chunk.toString("utf8");
    });

    proc.on("error", reject);

    proc.on("close", code => {
      if (code !== 0) {
        return reject(
          new Error(`Python analyzer exited with code ${code}\n${err}`)
        );
      }
      try {
        const parsed = JSON.parse(out);
        resolve(parsed);
      } catch (e) {
        reject(new Error("Failed to parse analyzer JSON: " + e));
      }
    });
  });
}

const server = new Server(
  { name: "ga-audio-analyzer", version: "0.1.0" },
  { tools: [analyzeTool] }
);

server.setRequestHandler<CallToolRequest, CallToolResult>(
  "tools/call",
  async (req, res) => {
    const { name, arguments: args } = req.params;

    if (name !== analyzeTool.name) {
      throw new Error(`Unknown tool: ${name}`);
    }

    const path = String(args?.path ?? "");
    const stats = await fs.stat(path).catch(() => null);
    if (!stats || !stats.isFile()) {
      throw new Error(`File not found: ${path}`);
    }

    const result = await runPythonAnalyze(path);

    await res.result({
      content: [{ type: "json", data: result }]
    });
  }
);

server.start();
