export interface APIResponse<T> {
    status: number;
    message: string;
    data: T;
    errors: string[];
  }
  