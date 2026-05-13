import { appendFile, mkdir } from 'node:fs/promises';
import path from 'node:path';

export type LogCategory = 'runtime' | 'sync' | 'error';
export type LogLevel = 'INFO' | 'WARN' | 'ERROR';

const fileByCategory: Record<LogCategory, string> = {
  runtime: 'runtime.log',
  sync: 'sync.log',
  error: 'error.log'
};

export class FileLogger {
  constructor(private logDirectory: string) {}

  setLogDirectory(logDirectory: string): void {
    this.logDirectory = logDirectory;
  }

  getLogDirectory(): string {
    return this.logDirectory;
  }

  async info(category: LogCategory, message: string): Promise<void> {
    await this.write(category, 'INFO', message);
  }

  async warn(category: LogCategory, message: string): Promise<void> {
    await this.write(category, 'WARN', message);
  }

  async error(message: string): Promise<void> {
    await this.write('error', 'ERROR', message);
  }

  async write(category: LogCategory, level: LogLevel, message: string): Promise<void> {
    await mkdir(this.logDirectory, { recursive: true });
    const line = `${new Date().toISOString()} [${level}] ${message}\n`;
    await appendFile(path.join(this.logDirectory, fileByCategory[category]), line, 'utf8');
  }
}
