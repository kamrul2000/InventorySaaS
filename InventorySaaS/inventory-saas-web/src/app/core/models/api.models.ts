export interface PaginatedList<T> {
  items: T[];
  pageNumber: number;
  totalPages: number;
  totalCount: number;
  hasPreviousPage: boolean;
  hasNextPage: boolean;
}

export interface ProblemResponse {
  type: string;
  title: string;
  status: number;
  errors?: { [key: string]: string[] };
  correlationId?: string;
}
