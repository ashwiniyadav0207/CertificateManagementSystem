import { NextResponse } from 'next/server';
import { getEventDetail } from '@/lib/server/certificates';

export const dynamic = 'force-dynamic';

export async function GET(
  request: Request,
  { params }: { params: Promise<{ eventId: string }> }
) {
  try {
    const { eventId } = await params;
    const { searchParams } = new URL(request.url);
    const page = parseInt(searchParams.get('page') || '1', 10);
    const pageSize = parseInt(searchParams.get('pageSize') || '50', 10);

    const detail = getEventDetail(eventId, page, pageSize);
    if (!detail) {
      return NextResponse.json({ error: `Event '${eventId}' not found.` }, { status: 404 });
    }

    return NextResponse.json(detail);
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
