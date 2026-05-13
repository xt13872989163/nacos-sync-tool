import { access, mkdir } from 'node:fs/promises';
import { constants } from 'node:fs';
import path from 'node:path';

export function resolveDefaultLogDirectory(
  platform: NodeJS.Platform,
  userDataPath: string,
  dDriveWritable: boolean
): string {
  if (platform === 'win32' && dDriveWritable) {
    return 'D:\\NacosSyncTool\\logs';
  }

  return path.join(userDataPath, 'logs');
}

export async function isDirectoryWritable(directory: string): Promise<boolean> {
  try {
    await mkdir(directory, { recursive: true });
    await access(directory, constants.W_OK);
    return true;
  } catch {
    return false;
  }
}

export async function resolveInitialLogDirectory(
  platform: NodeJS.Platform,
  userDataPath: string
): Promise<string> {
  const dDriveWritable = platform === 'win32' ? await isDirectoryWritable('D:\\') : false;
  return resolveDefaultLogDirectory(platform, userDataPath, dDriveWritable);
}
