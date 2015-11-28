---
title: This is my title
layout: post
---

Here is my page.

{% for release in site.github.releases %}
  * {{ release.tag_name }}
{% endfor %}