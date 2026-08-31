export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  hasGoogleLogin: boolean;
}

export interface Subscription {
  planKey: string;
  status: string;
  isEntitled: boolean;
  trialEndsAt: string | null;
  currentPeriodEndsAt: string | null;
  cancelAtPeriodEnd: boolean;
  paymentProvider: string | null;
}

export interface PortfolioSummary {
  id: number;
  name: string;
  strategyName: string | null;
  marketValue: number;
  dayChange: number;
  dayChangePercent: number;
  positionCount: number;
  updatedAt: string | null;
}

export interface PortfolioPosition {
  id: number;
  symbol: string;
  description: string | null;
  quantity: number;
  averagePrice: number | null;
  currentPrice: number | null;
  marketValue: number;
  unrealizedGain: number;
  unrealizedGainPercent: number;
}

export interface PortfolioDetails {
  id: number;
  name: string;
  description: string | null;
  strategyName: string | null;
  marketValue: number;
  totalCost: number;
  unrealizedGain: number;
  unrealizedGainPercent: number;
  positions: PortfolioPosition[];
}

export interface McpTokenSummary {
  id: number;
  displayName: string;
  prefix: string;
  createdAt: string;
  lastUsedAt: string | null;
  expiresAt: string | null;
  isRevoked: boolean;
}

export interface McpTokenCreated extends McpTokenSummary {
  token: string;
}

export interface ProblemDetails {
  title?: string;
  detail?: string;
  errors?: Record<string, string[]>;
}

export interface CheckoutResponse {
  redirectUrl: string;
  sessionId: string;
}
