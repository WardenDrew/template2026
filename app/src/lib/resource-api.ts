import { apiGetJson, apiPostJson } from "@/lib/api";

export type Resource = {
  id: string;
  name: string;
  description: string | null;
  createdAt: string;
};

export type ResourceDetails = {
  name: string;
  description: string | null;
};

export async function listResources() {
  return await apiGetJson<Resource[]>("/resources");
}

export async function createResource(request: ResourceDetails) {
  return await apiPostJson<Resource>("/resources", request);
}
