import { NextResponse } from 'next/server';
import { generateToken } from '@/lib/server/auth';

export const dynamic = 'force-dynamic';

export async function POST(request: Request) {
  try {
    const { refreshToken } = await request.json();
    if (!refreshToken) {
      return NextResponse.json({ error: 'Refresh token is required.' }, { status: 400 });
    }

    const accessToken = generateToken();
    const newRefreshToken = generateToken();

    return NextResponse.json({
      accessToken,
      refreshToken: newRefreshToken,
    });
  } catch (error: any) {
    return NextResponse.json({ error: error.message }, { status: 500 });
  }
}
