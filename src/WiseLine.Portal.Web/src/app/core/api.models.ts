export interface CurrentUser {
  id: string;
  email: string;
  displayName: string;
  hasGoogleLogin: boolean;
  emailConfirmed: boolean;
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
  id: string;
  name: string;
  strategyName: string | null;
  marketValue: number;
  dayChange: number;
  dayChangePercent: number;
  positionCount: number;
  updatedAt: string | null;
}

export interface PortfolioPosition {
  id: string;
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
  id: string;
  name: string;
  description: string | null;
  strategyName: string | null;
  marketValue: number;
  totalCost: number;
  unrealizedGain: number;
  unrealizedGainPercent: number;
  positions: PortfolioPosition[];
  strategies: PortfolioStrategy[];
}

export interface PortfolioStrategy {
  id: string;
  accountId: string | null;
  name: string;
  type: string;
  rule: unknown;
  scope: 'portfolio' | 'global';
  updatedAt: string | null;
}

export interface OAuthConnection {
  id: string;
  clientId: string;
  displayName: string;
  scopes: string[];
  authorizedAt: string | null;
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
