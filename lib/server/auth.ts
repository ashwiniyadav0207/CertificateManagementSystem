import crypto from "node:crypto"
import { getDb } from "./db"

export interface UserRecord {
  id: number
  name: string
  email: string
  password_hash: string
  role: string
  created_at: string
}

export interface UserInfo {
  id: number
  name: string
  email: string
  role: string
}

export function hashPassword(password: string): string {
  const salt = crypto.randomBytes(16)
  const hash = crypto.pbkdf2Sync(password, salt, 100000, 32, "sha256")
  return `${salt.toString("base64")}:${hash.toString("base64")}`
}

export function verifyPassword(password: string, storedHash: string): boolean {
  const parts = storedHash.split(":")
  if (parts.length !== 2) return false
  try {
    const salt = Buffer.from(parts[0], "base64")
    const expectedHash = Buffer.from(parts[1], "base64")
    const actualHash = crypto.pbkdf2Sync(password, salt, 100000, 32, "sha256")
    return crypto.timingSafeEqual(actualHash, expectedHash)
  } catch {
    return false
  }
}

export function generateToken(): string {
  return crypto.randomBytes(32).toString("hex")
}

export function findUserByEmail(email: string): UserRecord | null {
  const db = getDb()
  const stmt = db.prepare("SELECT id, name, email, password_hash, role, created_at FROM users WHERE lower(email) = lower(?) LIMIT 1")
  const row = stmt.get(email.trim()) as UserRecord | undefined
  return row ?? null
}

export function createUser(name: string, email: string, passwordHash: string, role: string = "admin"): UserRecord {
  const db = getDb()
  const now = new Date().toISOString()
  const stmt = db.prepare("INSERT INTO users (name, email, password_hash, role, created_at) VALUES (?, ?, ?, ?, ?)")
  const result = stmt.run(name.trim(), email.trim(), passwordHash, role, now)
  const id = Number(result.lastInsertRowid)
  return {
    id,
    name: name.trim(),
    email: email.trim(),
    password_hash: passwordHash,
    role,
    created_at: now,
  }
}
