import { NextResponse } from 'next/server';
import { getEventSummaries } from '@/lib/server/certificates';

export const dynamic = 'force-dynamic';

export async function GET() {
  try {
    const events = getEventSummaries();
    return NextResponse.json(events);
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
