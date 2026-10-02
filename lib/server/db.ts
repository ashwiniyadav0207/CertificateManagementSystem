import { DatabaseSync } from "node:sqlite"
import path from "node:path"
import fs from "node:fs"

const dbPath = path.resolve(process.cwd(), "data/certificates.db")

let dbInstance: DatabaseSync | null = null

export function getDb(): DatabaseSync {
  if (!dbInstance) {
    fs.mkdirSync(path.dirname(dbPath), { recursive: true })
    dbInstance = new DatabaseSync(dbPath)
    dbInstance.exec(`
      PRAGMA journal_mode = WAL;
      PRAGMA synchronous = NORMAL;
      PRAGMA foreign_keys = ON;
      PRAGMA busy_timeout = 10000;
    `)
  }
  return dbInstance
}
