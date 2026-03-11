export interface PaginatedList<T> {
  items: T[];
  pageNumber: number;
  totalPages: number;
  totalCount: number;
  hasPrevious: boolean;
  hasNext: boolean;
}

export interface ApiResult<T> {
  isSuccess: boolean;
  value?: T;
  errors: string[];
}

export interface ProblemResponse {
  type: string;
  title: string;
  status: number;
  errors?: { [key: string]: string[] };
  correlationId?: string;
}
